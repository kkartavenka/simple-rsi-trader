using Accord.Math.Optimization;
using CommonLib.Models;
using simple_rsi_trader.Models;
using System;
using System.Collections.Generic;
using static simple_rsi_trader.Classes.OperationClass;
using static simple_rsi_trader.Models.ParametersModel;

namespace simple_rsi_trader.Classes
{
    public class OptimizerClass
    {
        public enum ExecutionType : short { Train = 0, Test = 1 };

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

            if (_roundOrder) {
                weights[(int)OptimizingParameters.StopLoss] = Math.Round(weights[(int)OptimizingParameters.StopLoss], _roundPoint);
                weights[(int)OptimizingParameters.TakeProfit] = Math.Round(weights[(int)OptimizingParameters.TakeProfit], _roundPoint);
            }

            for (int i = 0; i < _size; i++) {
                if (_sequences[i].CheckActivation(weights, _parameter)) {
                    double limitOrder = _sequences[i].GetLimitOrder(weights, _parameter, _roundOrder, _roundPoint);
                    
                    OrderModel order = new(
                        endPeriodClosePrice: _sequences[i].EndPeriodClosePrice,
                        order: limitOrder,
                        firstLow: _sequences[i].FirstPeriodLowPrice,
                        firstHigh: _sequences[i].FirstPeriodHighPrice,
                        lowestPrice: _sequences[i].LowestPrice,
                        highestPrice: _sequences[i].HighestPrice,
                        nonFirstHighestPrice: _sequences[i].NonFirstHighestPrice,
                        nonFirstLowestPrice: _sequences[i].NonFirstLowestPrice);

                    (double profit, ActionOutcome outcome) = order.AssessProfitFromOrder(
                        operation: _parameter.Operation,
                        stopLoss: weights[(int)OptimizingParameters.StopLoss],
                        takeProfit: weights[(int)OptimizingParameters.TakeProfit],
                        commission: _commission); // GetProfitFromOrder(order, weights[(int)OptimizingParameters.StopLoss], weights[(int)OptimizingParameters.TakeProfit]);

                    switch (outcome) {
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

        public void LoadSequence(SequenceClass[] sequences) => _sequences = sequences;

        public void Optimize(double minTrainingScore, double preselectSize) {
            IsSuccess = false;
            Performance.Add(_executionType, new());

            _size = (int)(_sequences.Length * preselectSize);
            _executionType = ExecutionType.Train;
            _parameter.ToOptimizableArray();

            double preTrainScore = Evaluate(_parameter.OptimizableArray);
            if (preTrainScore > minTrainingScore) {
                _size = _sequences.Length;
                function = Evaluate;

                var solver = new NelderMead(numberOfVariables: _parameter.ParametersCount) {
                    Function = function,
                };
                solver.Maximize(_parameter.OptimizableArray);

                _roundOrder = true;
                Evaluate(solver.Solution);

                if (Performance[_executionType].Profit > minTrainingScore && double.IsFinite(Performance[_executionType].WinRate) && Performance[ExecutionType.Train].Profit < 1000) {
                    IsSuccess = true;
                    _parameter.ToModel(solver.Solution);
                }
            }

        }

        public void Validate() {
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
