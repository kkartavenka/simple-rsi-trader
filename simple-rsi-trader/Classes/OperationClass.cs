using Accord.Statistics;
using Accord.Statistics.Models.Regression.Linear;
using CommonLib.Models;
using simple_rsi_trader.Models;
using System;
using static CommonLib.Enums.Enums;

namespace simple_rsi_trader.Classes
{
    public static class OperationClass
    {
        public enum ActionOutcome : short { Success = 1, Failed = 0, NoAction = -1 };

        public static (double profit, ActionOutcome outcome) AssessProfitFromOrder(this OrderModel prediction, OperationType operation, double stopLoss, double takeProfit, double commission) {
            double halfOfCommission = commission / 2d;

            if (operation == OperationType.Buy) {
                if (prediction.Order > prediction.FirstLow + halfOfCommission && prediction.Order - stopLoss < prediction.LowestPrice - halfOfCommission) {

                    double knownDistance = prediction.EndPeriodClosePrice - prediction.Order - halfOfCommission;
                    double maxDistance = knownDistance;

                    if (prediction.NonFirstHighestPrice > 0)
                        maxDistance = prediction.HighestPrice - prediction.Order - halfOfCommission;

                    double profit = maxDistance < takeProfit ? knownDistance : takeProfit - halfOfCommission;

                    if (profit > 0)
                        return (profit, ActionOutcome.Success);
                    else
                        return (profit, ActionOutcome.Failed);
                }
                else if (prediction.Order - stopLoss >= prediction.FirstLow + halfOfCommission)
                    return ((-1) * (stopLoss + halfOfCommission), ActionOutcome.Failed);
            }
            else if (operation == OperationType.Sell) {
                if (prediction.Order < prediction.FirstHigh - halfOfCommission  && prediction.Order + stopLoss > prediction.HighestPrice + halfOfCommission) {

                    double knownDistance = prediction.Order - prediction.EndPeriodClosePrice - halfOfCommission;
                    double maxDistance = knownDistance;

                    if (prediction.NonFirstLowestPrice > 0)
                        maxDistance = prediction.Order - prediction.NonFirstLowestPrice - halfOfCommission;

                    double profit = maxDistance < takeProfit ? knownDistance : takeProfit - halfOfCommission;

                    if (profit > 0)
                        return (profit, ActionOutcome.Success);
                    else
                        return (profit, ActionOutcome.Failed);
                }
                else if (prediction.Order + stopLoss <= prediction.FirstHigh + halfOfCommission)
                    return ((-1) * (stopLoss + commission / 2d), ActionOutcome.Failed);
            }
            return (0, ActionOutcome.NoAction);

        }

        public static bool CheckActivation(this SequenceClass sequence, double[] weights, ParametersModel parameter) {
            bool returnVar = true;
            double[] rsiSequence = sequence.RsiSequence[^parameter.IndicatorLastPointSequence..];
            double[] x = new double[rsiSequence.Length];
            
            for (int i = 0; i < x.Length; i++)
                x[i] = i;

            for (int i = 0; i < rsiSequence.Length; i++) {
                if (parameter.Operation == OperationType.Buy) {
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

            if (returnVar) {
                OrdinaryLeastSquares ols = new OrdinaryLeastSquares() { UseIntercept = true };
                SimpleLinearRegression reg = ols.Learn(x, rsiSequence);

                if (reg.Slope > 0 && parameter.Operation == OperationType.Sell) {
                    double[] expectedRsi = reg.Transform(x);
                    double r2 = expectedRsi.RSquared(rsiSequence);

                    if (r2 < weights[(int)OptimizingParameters.RSquaredCutOff])
                        returnVar = false;
                }
                else if (reg.Slope < 0 && parameter.Operation == OperationType.Buy) {
                    double[] expectedRsi = reg.Transform(x);
                    double r2 = expectedRsi.RSquared(rsiSequence);

                    if (r2 < weights[(int)OptimizingParameters.RSquaredCutOff])
                        returnVar = false;
                }
                else
                    returnVar = false;
            }

            return returnVar;
        }

        public static double GetLimitOrder(this SequenceClass sequence, double[] weights, ParametersModel parameter, bool round, int roundPoint, bool isTraining) {
            double limitOrder;

            if (parameter.Operation == OperationType.Sell) {
                limitOrder = sequence.CurrentClosePrice
                    + sequence.CurrentClosePrice * weights[(int)OptimizingParameters.Offset0]
                    - weights[(int)OptimizingParameters.Offset1] * sequence.RsiSequence[^1]
                    /*- weights[(int)OptimizingParameters.Offset2] * sequence.RsiSequence[^1] * sequence.RsiSequence[^1]*/;

                if (limitOrder < sequence.CurrentClosePrice && !isTraining)
                    limitOrder = sequence.CurrentClosePrice;
            }
            else {
                limitOrder = sequence.CurrentClosePrice
                    - sequence.CurrentClosePrice * weights[(int)OptimizingParameters.Offset0]
                    + weights[(int)OptimizingParameters.Offset1] * (100 - sequence.RsiSequence[^1])
                    /*+weights[(int)OptimizingParameters.Offset2] * (100 - sequence.RsiSequence[^1]) * (100 - sequence.RsiSequence[^1])*/;

                if (limitOrder > sequence.CurrentClosePrice && !isTraining)
                    limitOrder = sequence.CurrentClosePrice;
            }

            if (round)
                limitOrder = Math.Round(limitOrder, roundPoint);

            return limitOrder;
        }

        public static PredictionStruct GetOrder(this SequenceClass sequence, double[] weights, ParametersModel parameter, int roundPoint, double score) {
            double limitOrder = sequence.GetLimitOrder(weights, parameter, true, roundPoint, isTraining: false);

            if (parameter.Operation == OperationType.Sell)
                return new PredictionStruct(
                    limitOrder: limitOrder,
                    stopLoss: Math.Round(limitOrder + weights[(int)OptimizingParameters.StopLoss], roundPoint),
                    takeProfit: Math.Round(limitOrder - weights[(int)OptimizingParameters.TakeProfit], roundPoint),
                    takeProfitDistance: Math.Round(weights[(int)OptimizingParameters.TakeProfit], roundPoint),
                    stopLossDistance: Math.Round(weights[(int)OptimizingParameters.StopLoss], roundPoint),
                    score: score);
            else
                return new PredictionStruct(
                    limitOrder: limitOrder,
                    stopLoss: Math.Round(limitOrder - weights[(int)OptimizingParameters.StopLoss], roundPoint),
                    takeProfit: Math.Round(limitOrder + weights[(int)OptimizingParameters.TakeProfit], roundPoint),
                    stopLossDistance: Math.Round(weights[(int)OptimizingParameters.StopLoss], roundPoint),
                    takeProfitDistance: Math.Round(weights[(int)OptimizingParameters.TakeProfit], roundPoint),
                    score: score);
        }

        private static double RSquared(this double[] expected, double[] observed) {
            double yMean = observed.Mean();
            double ssTot = 0;
            double ssRes = 0;

            for (int i = 0; i < observed.Length; i++) {
                ssTot += Math.Pow((observed[i] - yMean), 2);
                ssRes += Math.Pow((observed[i] - expected[i]), 2);
            }

            return 1 - (ssRes / ssTot);
        }
    }
}
