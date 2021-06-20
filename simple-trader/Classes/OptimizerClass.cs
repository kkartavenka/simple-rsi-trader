using Accord.Math.Optimization;
using CommonLib.Enums;
using CommonLib.Models;
using simple_trader.Enums;
using simple_trader.Models;
using System;
using System.Collections.Generic;
using static simple_trader.Classes.OperationClass;

namespace simple_trader.Classes
{
    public class OptimizerClass
    {
        public enum ExecutionType : short { Train = 0, Test = 1 };

        private const double _minWinRate = 0.01;
        private readonly ParametersModel _parameter;
        private readonly double _commission;
        private readonly int _roundPoint;
        private bool _roundOrder = false;
        private ExecutionType _executionType;
        private bool _isTraining;
        ReadOnlyMemory<SequenceClass> _sequenceMemory;

        public OptimizerClass(ReadOnlyMemory<SequenceClass> sequences, ParametersModel parameter, double commission, int roundPoint) {
            _parameter = parameter;
            _isTraining = true;
            _roundPoint = roundPoint;
            _commission = commission;
            _sequenceMemory = sequences;
        }

        Func<double[], double> function;
        public Dictionary<ExecutionType, PerformanceModel> Performance { get; private set; } = new();

        private Span<double> EnforceConstrains(Span<double> weights) {
            if (weights[(int)OptimizingParameters.StopLoss] < _parameter.StopLoss.Range.Min)
                weights[(int)OptimizingParameters.StopLoss] = _parameter.StopLoss.Range.Min;
            else if (weights[(int)OptimizingParameters.StopLoss] > _parameter.StopLoss.Range.Max)
                weights[(int)OptimizingParameters.StopLoss] = _parameter.StopLoss.Range.Max;

            if (weights[(int)OptimizingParameters.TakeProfit] < _parameter.TakeProfit.Range.Min)
                weights[(int)OptimizingParameters.TakeProfit] = _parameter.TakeProfit.Range.Min;

            if (_parameter.Operation == OperationType.Buy) {
                if (weights[(int)OptimizingParameters.Slope] < _parameter.SlopeLimits.Range.Min)
                    weights[(int)OptimizingParameters.Slope] = _parameter.SlopeLimits.Range.Min;

            }
            else {
                if (weights[(int)OptimizingParameters.Slope] > _parameter.SlopeLimits.Range.Max)
                    weights[(int)OptimizingParameters.Slope] = _parameter.SlopeLimits.Range.Max;
            }

            if (weights[(int)OptimizingParameters.RSquaredCutOff] < _parameter.RSquaredCutOff.Range.Min)
                weights[(int)OptimizingParameters.RSquaredCutOff] = _parameter.RSquaredCutOff.Range.Min;

            return weights;
        }

        private double Evaluate(double[] weights) {
            var weightsSpan = new Span<double>(weights);

            if (_executionType == ExecutionType.Train) {
                Performance[_executionType] = new();
                weightsSpan = EnforceConstrains(weightsSpan);
            }

            ReadOnlySpan<SequenceClass> sequenceSpan = _sequenceMemory.Span;

            for (int i = 0; i < _sequenceMemory.Length; i++) {
                
                bool condition = (sequenceSpan[i].AllowBuy && _parameter.Operation == OperationType.Buy) || (sequenceSpan[i].AllowSell && _parameter.Operation == OperationType.Sell);
                
                if (condition) {
                    var activatedStatus = sequenceSpan[i].CheckActivation(slope: weightsSpan[(int)OptimizingParameters.Slope], rsquared: weightsSpan[(int)OptimizingParameters.RSquared], parameter: _parameter);

                    if (activatedStatus.Activated) {
                        double limitOrder = sequenceSpan[i].GetLimitOrder(
                            weights: weightsSpan,
                            parameter: _parameter,
                            round: _roundOrder,
                            roundPoint: _roundPoint,
                            isTraining: _isTraining,
                            activationStatus: activatedStatus);

                        OrderModel order = new(
                            endPeriodClosePrice: sequenceSpan[i].EndPeriodClosePrice,
                            order: limitOrder,
                            firstLow: sequenceSpan[i].FirstPeriodLowPrice,
                            firstHigh: sequenceSpan[i].FirstPeriodHighPrice,
                            lowestPrice: sequenceSpan[i].LowestPrice,
                            highestPrice: sequenceSpan[i].HighestPrice,
                            nonFirstHighestPrice: sequenceSpan[i].NonFirstHighestPrice,
                            nonFirstLowestPrice: sequenceSpan[i].NonFirstLowestPrice);

                        (double profit, ActionOutcome outcome) = order.AssessProfitFromOrder(
                            operation: _parameter.Operation,
                            stopLoss: _roundOrder ? Math.Round(weightsSpan[(int)OptimizingParameters.StopLoss] * sequenceSpan[i].CurrentClosePrice, _roundPoint) : weightsSpan[(int)OptimizingParameters.StopLoss] * sequenceSpan[i].CurrentClosePrice,
                            takeProfit: _roundOrder ? Math.Round(weightsSpan[(int)OptimizingParameters.TakeProfit] * sequenceSpan[i].CurrentClosePrice, _roundPoint) : weightsSpan[(int)OptimizingParameters.TakeProfit] * sequenceSpan[i].CurrentClosePrice,
                            commission: _commission);

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
            }

            Performance[_executionType].CalculateMetrics(_commission, sequenceSpan.Length);
            return Performance[_executionType].Score;
        }

        public void LoadSequence(ReadOnlyMemory<SequenceClass> sequences, bool isTraining) {
            _sequenceMemory = sequences;
            _isTraining = isTraining;
        }

        public (double preTrainScore, double postTrainScore) Optimize(double minTrainingScore) {
            IsSuccess = false;
            Performance.Add(_executionType, new());

            _executionType = ExecutionType.Train;
            _parameter.ToOptimizableArray();

            double preTrainScore = Evaluate(_parameter.OptimizableArray);
            double postTrainScore = 0;
            if (preTrainScore > minTrainingScore) {
                function = Evaluate;

                var solver = new NelderMead(numberOfVariables: _parameter.ParametersCount) {
                    Function = function,
                     
                };
                bool converged = solver.Maximize(_parameter.OptimizableArray);

                if (!converged)
                    return (0, 0);

                _roundOrder = true;
                postTrainScore = Evaluate(solver.Solution);

                if (Performance[_executionType].Profit > minTrainingScore && double.IsFinite(Performance[_executionType].WinRate) && Performance[ExecutionType.Train].Profit < 1000 && (double)Performance[_executionType].WinCount / _sequenceMemory.Length > _minWinRate) {
                    IsSuccess = true;
                    _parameter.ToModel(solver.Solution);
                }
            }

            return (preTrainScore, postTrainScore);
        }

        public void Validate() {
            IsSuccess = false;

            _executionType = ExecutionType.Test;
            Performance.Add(_executionType, new());

            _parameter.ToOptimizableArray();

            Evaluate(_parameter.OptimizableArray);

            if (Performance[ExecutionType.Test].Profit > 0 && double.IsFinite(Performance[_executionType].WinRate) && Performance[ExecutionType.Test].Profit < 1000) {
                IsSuccess = true;
                //Console.WriteLine($"{_parameter.Operation}\tTraining: {(Performance[ExecutionType.Train].Profit):N2}\tTesting: {(Performance[ExecutionType.Test].Profit):N2}\t SL: {_parameter.StopLoss.Value}\tTP: {_parameter.TakeProfit.Value}\tIterations: {_iteration}");
            }
        }

        public bool IsSuccess { get; private set; }
    }
}
