using Accord.Statistics;
using Accord.Statistics.Models.Regression.Linear;
using CommonLib.Models;
using simple_rsi_trader.Models;
using System;
using static CommonLib.Enums.Enums;
using CommonLib.Extensions;

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

        public static ActivationReturnStruct CheckActivation(this SequenceClass sequence, ReadOnlySpan<double> weights, ParametersModel parameter) {
            double[] rsiSequence = sequence.RsiSequence[^parameter.IndicatorLastPointSequence..];
            var rsiSequenceSpan = new Span<double>(rsiSequence);
            double[] x = new double[rsiSequence.Length];

            for (int i = 0; i < x.Length; i++)
                x[i] = i;

            int allowedPass = (int)Math.Round(rsiSequence.Length * 0.05);
            int halfWay = (int)(rsiSequence.Length * 0.5);

            for (int i = 0; i < rsiSequence.Length; i++) {
                if (parameter.Operation == OperationType.Buy) {
                    if (weights[(int)OptimizingParameters.Weight0] - weights[(int)OptimizingParameters.Weight1] * i < rsiSequenceSpan[i]) {
                        if (allowedPass <= 0 || halfWay > i)
                            return default;
                        allowedPass--;
                    }
                }
                else {
                    if (weights[(int)OptimizingParameters.Weight0] + weights[(int)OptimizingParameters.Weight1] * i > rsiSequenceSpan[i]) {
                        if (allowedPass <= 0 || halfWay > i)
                            return default;
                        allowedPass--;
                    }
                }
            }

            OrdinaryLeastSquares ols = new OrdinaryLeastSquares() { UseIntercept = true };
            SimpleLinearRegression reg = ols.Learn(x, rsiSequence);
            double r2;

            if (reg.Slope > 0 && parameter.Operation == OperationType.Sell) {
                var expectedRsi = new ReadOnlySpan<double>(reg.Transform(x));
                r2 = expectedRsi.RSquared(rsiSequence);

                if (r2 < weights[(int)OptimizingParameters.RSquaredCutOff])
                    return default;
            }
            else if (reg.Slope < 0 && parameter.Operation == OperationType.Buy) {
                var expectedRsi = new ReadOnlySpan<double>(reg.Transform(x));
                r2 = expectedRsi.RSquared(rsiSequence);

                if (r2 < weights[(int)OptimizingParameters.RSquaredCutOff])
                    return default;
            }
            else
                return default;

            return new ActivationReturnStruct(activated: true, slope: reg.Slope, rSquared: r2);
        }

        public static double GetLimitOrder(this SequenceClass sequence, ReadOnlySpan<double> weights, ParametersModel parameter, bool round, int roundPoint, bool isTraining, ActivationReturnStruct activationStatus) {
            double limitOrder;

            if (parameter.Operation == OperationType.Sell) {
                limitOrder = sequence.CurrentClosePrice
                    + sequence.CurrentClosePrice * (weights[(int)OptimizingParameters.Offset0]
                    - weights[(int)OptimizingParameters.Offset1] * sequence.RsiSequence[^1]
                    - weights[(int)OptimizingParameters.Offset2] * Math.Pow(sequence.RsiSequence[^1], 2)
                    - weights[(int)OptimizingParameters.Offset3] * Math.Pow(sequence.RsiSequence[^1], 3)
                    + sequence.ChangeEMA * weights[(int)OptimizingParameters.ChangeEmaCorrection]
                    + activationStatus.Slope * activationStatus.RSquared * weights[(int)OptimizingParameters.RsiSlopeFitCorrection]);

                if (limitOrder < sequence.CurrentClosePrice && !isTraining)
                    limitOrder = sequence.CurrentClosePrice;
            }
            else {
                double invertedRsi = 100 - sequence.RsiSequence[^1];

                limitOrder = sequence.CurrentClosePrice
                    - sequence.CurrentClosePrice * (weights[(int)OptimizingParameters.Offset0]
                    + weights[(int)OptimizingParameters.Offset1] * invertedRsi
                    + weights[(int)OptimizingParameters.Offset2] * Math.Pow(invertedRsi, 2)
                    + weights[(int)OptimizingParameters.Offset3] * Math.Pow(invertedRsi, 3)
                    + sequence.ChangeEMA * weights[(int)OptimizingParameters.ChangeEmaCorrection]
                    - activationStatus.Slope * activationStatus.RSquared * weights[(int)OptimizingParameters.RsiSlopeFitCorrection]);

                if (limitOrder > sequence.CurrentClosePrice && !isTraining)
                    limitOrder = sequence.CurrentClosePrice;
            }

            if (round)
                limitOrder = Math.Round(limitOrder, roundPoint);

            return limitOrder;
        }

        public static PredictionStruct GetOrder(this SequenceClass sequence, double[] weights, ParametersModel parameter, int roundPoint, double score, ActivationReturnStruct activationStatus) {
            double limitOrder = sequence.GetLimitOrder(weights, parameter, true, roundPoint, isTraining: false, activationStatus: activationStatus);

            if (parameter.Operation == OperationType.Sell)
                return new PredictionStruct(
                    limitOrder: limitOrder,
                    stopLoss: Math.Round(limitOrder + sequence.CurrentClosePrice * weights[(int)OptimizingParameters.StopLoss], roundPoint),
                    takeProfit: Math.Round(limitOrder - sequence.CurrentClosePrice * weights[(int)OptimizingParameters.TakeProfit], roundPoint),
                    takeProfitDistance: Math.Round(sequence.CurrentClosePrice * weights[(int)OptimizingParameters.TakeProfit], roundPoint),
                    stopLossDistance: Math.Round(sequence.CurrentClosePrice * weights[(int)OptimizingParameters.StopLoss], roundPoint),
                    score: score);
            else
                return new PredictionStruct(
                    limitOrder: limitOrder,
                    stopLoss: Math.Round(limitOrder - sequence.CurrentClosePrice * weights[(int)OptimizingParameters.StopLoss], roundPoint),
                    takeProfit: Math.Round(limitOrder + sequence.CurrentClosePrice * weights[(int)OptimizingParameters.TakeProfit], roundPoint),
                    stopLossDistance: Math.Round(sequence.CurrentClosePrice * weights[(int)OptimizingParameters.StopLoss], roundPoint),
                    takeProfitDistance: Math.Round(sequence.CurrentClosePrice * weights[(int)OptimizingParameters.TakeProfit], roundPoint),
                    score: score);
        }

        private static double RSquared(this ReadOnlySpan<double> expected, ReadOnlySpan<double> observed) {
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
