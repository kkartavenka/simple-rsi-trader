using Accord.Math.Optimization;
using CommonLib.Models;
using simple_rsi_trader.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static simple_rsi_trader.Models.ParametersModel;

namespace simple_rsi_trader.Classes
{
    public class OptimizerClass
    {
        private enum ActionOutcome : short { Success = 1, Failed = 0, NoAction = -1 };
        public enum ExecutionType : short { Train = 0, Test = 1 };

        private const double _preQualifyFraction = 0.5;
        private readonly ParametersModel _parameter;
        private SequenceClass[] _sequences;
        private int _size;
        private readonly double _commission;
        private readonly int _roundPoint;
        private bool _roundOrder = false;
        private int _iteration = -1;
        private ExecutionType _executionType;
        public OptimizerClass(SequenceClass[] sequences, ParametersModel parameter, double commission, int roundPoint)
        {
            _parameter = parameter;
            _roundPoint = roundPoint;
            _commission = commission;
            _sequences = sequences;
        }

        Func<double[], double> function;
        public Dictionary<ExecutionType, PerformanceModel> Performance { get; private set; } = new();

        private bool AllowOperation(SequenceClass sequence, double[] weights) {
            bool returnVar = true;
            double[] rsiSequence = sequence.RsiSequence[^_parameter.IndicatorLastPointSequence..];

            for (int i = 0; i < rsiSequence.Length; i++) {
                if (_parameter.Operation == OperationType.Buy) {
                    if (weights[(int)OptimizingParameters.Weight0] - weights[(int)OptimizingParameters.Weight1] * i < rsiSequence[i]) {
                        returnVar = false;
                        break;
                    }
                }
                else {
                    if (weights[(int)OptimizingParameters.Weight0] + weights[(int)OptimizingParameters.Weight1] * i > rsiSequence[i]) {
                        returnVar = false;
                        break;
                    }
                }
            }

            return returnVar;
        }

        private double[] EnforceConstrains(double[] weights) {
            double[] returnVar = weights;

            if (returnVar[(int)OptimizingParameters.StopLoss] < _parameter.StopLoss.Range.Min)
                returnVar[(int)OptimizingParameters.StopLoss] = _parameter.StopLoss.Range.Min;
            else if (returnVar[(int)OptimizingParameters.StopLoss] > _parameter.StopLoss.Range.Max)
                returnVar[(int)OptimizingParameters.StopLoss] = _parameter.StopLoss.Range.Max;

            if (returnVar[(int)OptimizingParameters.TakeProfit] < _parameter.TakeProfit.Range.Min)
                returnVar[(int)OptimizingParameters.TakeProfit] = _parameter.TakeProfit.Range.Min;
            else if (returnVar[(int)OptimizingParameters.TakeProfit] > _parameter.TakeProfit.Range.Max)
                returnVar[(int)OptimizingParameters.TakeProfit] = _parameter.TakeProfit.Range.Max;

            if(_parameter.Operation == OperationType.Buy) {
                if (returnVar[(int)OptimizingParameters.Weight0] > _parameter.RsiLimits.Range.Max)
                    returnVar[(int)OptimizingParameters.Weight0] = _parameter.RsiLimits.Range.Max;

                double recentRsiCut = returnVar[(int)OptimizingParameters.Weight0] - _parameter.IndicatorLastPointSequence * returnVar[(int)OptimizingParameters.Weight1];
                if (recentRsiCut > _parameter.RsiLimits.Range.Max)
                    returnVar[(int)OptimizingParameters.Weight1] = (_parameter.RsiLimits.Range.Max - returnVar[(int)OptimizingParameters.Weight0]) / _parameter.IndicatorLastPointSequence;
            }
            else {
                if (returnVar[(int)OptimizingParameters.Weight0] < _parameter.RsiLimits.Range.Min)
                    returnVar[(int)OptimizingParameters.Weight0] = _parameter.RsiLimits.Range.Min;

                double recentRsiCut = returnVar[(int)OptimizingParameters.Weight0] + _parameter.IndicatorLastPointSequence * returnVar[(int)OptimizingParameters.Weight1];
                if (recentRsiCut < _parameter.RsiLimits.Range.Min)
                    returnVar[(int)OptimizingParameters.Weight1] = (returnVar[(int)OptimizingParameters.Weight0] - _parameter.RsiLimits.Range.Min) / _parameter.IndicatorLastPointSequence;
            }


            return returnVar;
        }

        private double Evaluate(double[] weights) {
            _iteration++;

            if (_executionType == ExecutionType.Train) {
                Performance[_executionType] = new();
                weights = EnforceConstrains(weights);
            }

            for (int i = 0; i < _size; i++) {
                if (AllowOperation(sequence: _sequences[i], weights: weights)) {
                    double limitOrder;

                    if(_parameter.Operation == OperationType.Sell) {
                        limitOrder = _sequences[i].CurrentClosePrice
                            + weights[(int)OptimizingParameters.Offset0]
                            - weights[(int)OptimizingParameters.Offset1] * _sequences[i].RsiSequence[^1];
                    }
                    else {
                        limitOrder = _sequences[i].CurrentClosePrice
                            - weights[(int)OptimizingParameters.Offset0]
                            + weights[(int)OptimizingParameters.Offset1] * (100 - _sequences[i].RsiSequence[^1]);
                    }

                    if (_roundOrder)
                        limitOrder = Math.Round(limitOrder, _roundPoint);

                    OrderModel order = new (
                        close: _sequences[i].CurrentClosePrice,
                        order: limitOrder,
                        low: _sequences[i].LowPrice,
                        lowestPrice: _sequences[i].LowestPrice,
                        high: _sequences[i].HighestPrice,
                        highestPrice: _sequences[i].HighestPrice);

                    (double profit, ActionOutcome outcome) = GetProfitFromOrder(order, weights[(int)OptimizingParameters.StopLoss], weights[(int)OptimizingParameters.StopLoss]);

                    switch(outcome) {
                        case ActionOutcome.Failed:
                            Performance[_executionType].ActionCount++;
                            Performance[_executionType].LossCount++;
                            Performance[_executionType].Profit += profit;
                            break;
                        case ActionOutcome.NoAction:
                            Performance[_executionType].ActionCount++;
                            break;
                        case ActionOutcome.Success:
                            Performance[_executionType].ActionCount++;
                            Performance[_executionType].WinCount++;
                            Performance[_executionType].Profit += profit;
                            break;
                    }
                }
            }

            Performance[_executionType].CalculateMetrics(_commission, _size);
            return Performance[_executionType].Score;
        }

        private (double profit, ActionOutcome outcome) GetProfitFromOrder(OrderModel prediction, double stopLoss, double takeProfit) {
            if (_parameter.Operation == OperationType.Buy) {
                if (prediction.Order > prediction.Low + _commission / 2d && prediction.Order < prediction.LowestPrice - _commission / 2d + stopLoss) {
                    
                    double distance = prediction.Close - prediction.Order;

                    double profit = distance < takeProfit ? distance - _commission / 2d : takeProfit - _commission / 2d;

                    if (profit > 0)
                        return (profit, ActionOutcome.Success);
                    else
                        return (profit, ActionOutcome.Failed);
                }
                else if (prediction.Order >= prediction.Low + _commission / 2d + stopLoss)
                    return ((-1) * (stopLoss + _commission / 2d), ActionOutcome.Failed);
            }
            else {
                if (prediction.Order < prediction.High - _commission / 2d && prediction.Order > prediction.HighestPrice + _commission / 2d - stopLoss) {
                    
                    double distance = prediction.Order - prediction.Close;

                    double profit = distance < takeProfit ? distance - _commission / 2d : takeProfit - _commission / 2d;

                    if (profit > 0)
                        return (profit, ActionOutcome.Success);
                    else
                        return (profit, ActionOutcome.Failed);
                }
                else if (prediction.Order <= prediction.High + _commission / 2d - stopLoss)
                    return ((-1) * (stopLoss + _commission / 2d), ActionOutcome.Failed);
            }
            return (0, ActionOutcome.NoAction);
        }

        public void LoadSequence(SequenceClass[] sequences) => _sequences = sequences;

        public void Optimize(double minTrainingScore) {
            IsSuccess = false;
            Performance.Add(_executionType, new());

            _size = (int)(_sequences.Length * _preQualifyFraction);
            _executionType = ExecutionType.Train;
            _parameter.ToOptimizableArray();

            if (Evaluate(_parameter.OptimizableArray) > minTrainingScore) {
                _size = _sequences.Length;
                function = Evaluate;

                var solver = new NelderMead(numberOfVariables: _parameter.ParametersCount) {
                    Function = function,
                };
                solver.Maximize(_parameter.OptimizableArray);

                _roundOrder = true;
                Evaluate(solver.Solution);

                if (Performance[_executionType].Profit > 0 && double.IsFinite(Performance[_executionType].WinRate) && Performance[ExecutionType.Train].Profit < 1000) {
                    IsSuccess = true;
                    _parameter.ToModel(solver.Solution);
                }
            }

        }

        public void Test() {
            IsSuccess = false;

            _executionType = ExecutionType.Test;
            Performance.Add(_executionType, new());

            _size = _sequences.Length;

            _parameter.ToOptimizableArray();

            Evaluate(_parameter.OptimizableArray);

            if (Performance[ExecutionType.Test].Profit > 0 && double.IsFinite(Performance[_executionType].WinRate) && Performance[ExecutionType.Test].Profit < 1000) {
                IsSuccess = true;
                Console.WriteLine($"{_parameter.Operation}\tTraining: {(Performance[ExecutionType.Train].Profit):N2}\tTesting: {(Performance[ExecutionType.Test].Profit):N2}\t SL: {_parameter.StopLoss.Value}\tTP: {_parameter.TakeProfit.Value}\tIterations: {_iteration}");
            }
        }

        public bool IsSuccess { get; private set; }
    }
}
