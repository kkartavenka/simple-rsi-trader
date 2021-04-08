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
        private const double _preQualifyFraction = 0.3;
        private readonly ParametersModel _parameter;
        private readonly SequenceClass[] _sequences;
        private readonly int _size;
        private readonly double _commission;
        private readonly int _roundPoint;
        private bool _roundOrder = false;
        public OptimizerClass(SequenceClass[] sequences, ParametersModel parameter, double commission, int roundPoint)
        {
            _parameter = parameter;
            _roundPoint = roundPoint;
            _commission = commission;
            _sequences = sequences;
            _size = (int)(sequences.Length * _preQualifyFraction);
            
            parameter.ToOptimizableArray();
            if (Evaluate(parameter.OptimizableArray) > 0) {
                _size = sequences.Length;
                function = Evaluate;

                var solver = new NelderMead(numberOfVariables: parameter.ParametersCount) {
                    Function = function,
                };
                solver.Maximize(parameter.OptimizableArray);
                
                _roundOrder = true;
                Evaluate(solver.Solution);

                parameter.ToModel(solver.Solution);
            }
        }

        public PerformanceModel Performance { get; private set; }

        Func<double[], double> function;

        private double Evaluate(double[] weights) {

            Performance = new(); 
            
            for (int i = 0; i < _size; i++) {
                if (AllowOperation(sequence: _sequences[i], weights: weights)) {
                    double limitOrder = 0;

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
                            Performance.ActionCount++;
                            Performance.LossCount++;
                            Performance.Profit += profit;
                            break;
                        case ActionOutcome.NoAction:
                            Performance.ActionCount++;
                            break;
                        case ActionOutcome.Success:
                            Performance.ActionCount++;
                            Performance.WinCount++;
                            Performance.Profit += profit;
                            break;
                    }
                }
            }

            Performance.CalculateMetrics();
            Console.WriteLine(Performance.Profit);
            return Performance.Score;
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


        private bool AllowOperation(SequenceClass sequence, double[] weights) {
            bool returnVar = true;
            double[] rsiSequence = sequence.RsiSequence[^_parameter.IndicatorLastPointSequence..];

            for(int i = 0; i < rsiSequence.Length; i++) {
                if(_parameter.Operation == OperationType.Buy) {
                    if(weights[(int)OptimizingParameters.Weight0] + weights[(int)OptimizingParameters.Weight1] * i < rsiSequence[i]) {
                        returnVar = false;
                        break;
                    }
                }
                else {
                    if(weights[(int)OptimizingParameters.Weight0] + weights[(int)OptimizingParameters.Weight1] * i > rsiSequence[i]) {
                        returnVar = false;
                        break;
                    }
                }
            }

            return returnVar;
        }
    }
}
