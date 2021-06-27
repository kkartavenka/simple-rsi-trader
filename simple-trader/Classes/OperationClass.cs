using Accord.Statistics;
using CommonLib.Models;
using simple_trader.Models;
using System;
using CommonLib.Extensions;
using simple_trader.Enums;
using CommonLib.Enums;

namespace simple_trader.Classes
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

        public static ActivationReturnStruct CheckActivation(this SequenceClass sequence, double slope, double rsquared, ParametersModel parameter) {

            if (parameter.Operation == OperationType.Buy && sequence.ClosePriceSlope < slope && sequence.ClosePriceSlopeRSquared >= rsquared)
                return new ActivationReturnStruct(activated: true, slope: sequence.ClosePriceSlope, rSquared: sequence.ClosePriceSlopeRSquared);
            else if (parameter.Operation == OperationType.Sell && sequence.ClosePriceSlope > slope && sequence.ClosePriceSlopeRSquared >= rsquared)
                return new ActivationReturnStruct(activated: true, slope: sequence.ClosePriceSlope, rSquared: sequence.ClosePriceSlopeRSquared);

            return default;
        }

        public static double GetLimitOrder(this SequenceClass sequence, ReadOnlySpan<double> weights, ParametersModel parameter, bool round, int roundPoint, bool isTraining, ActivationReturnStruct activationStatus) {
            double limitOrder;

            if (parameter.Operation == OperationType.Sell) {
                limitOrder = sequence.CurrentClosePrice
                    + sequence.CurrentClosePrice * (weights[(int)OptimizingParameters.Offset0]
                    - weights[(int)OptimizingParameters.Offset1] * sequence.SmoothedSlope
                    - weights[(int)OptimizingParameters.Offset2] * Math.Pow(sequence.SmoothedSlope, 2)

                    - (sequence.IndicatorDirection * weights[(int)OptimizingParameters.Rsi0] + Math.Pow(sequence.IndicatorDirection, 2) * weights[(int)OptimizingParameters.Rsi1])
                    - (sequence.IndicatorMagnitude * weights[(int)OptimizingParameters.Mfi0] + Math.Pow(sequence.IndicatorMagnitude, 2) * weights[(int)OptimizingParameters.Mfi1])

                    + sequence.ChangeEMA * weights[(int)OptimizingParameters.ChangeEmaCorrection]
                    - sequence.SmoothedSlope * sequence.SmoothedSlopeRSquared * weights[(int)OptimizingParameters.SlopeRSquaredFitCorrection]);

                /*limitOrder = sequence.CurrentClosePrice
                    + sequence.CurrentClosePrice * (weights[(int)OptimizingParameters.Offset0]
                    - weights[(int)OptimizingParameters.Offset1] * sequence.SmoothedSlope
                    - weights[(int)OptimizingParameters.Offset2] * Math.Pow( sequence.SmoothedSlope, 2)

                    - (sequence.Rsi * weights[(int)OptimizingParameters.Rsi0] + Math.Pow(sequence.Rsi, 2) * weights[(int)OptimizingParameters.Rsi1])
                    - (sequence.Mfi * weights[(int)OptimizingParameters.Mfi0] + Math.Pow(sequence.Mfi, 2) * weights[(int)OptimizingParameters.Mfi1])
                    
                    + sequence.ChangeEMA * weights[(int)OptimizingParameters.ChangeEmaCorrection]
                    - sequence.SmoothedSlope * sequence.SmoothedSlopeRSquared * weights[(int)OptimizingParameters.SlopeRSquaredFitCorrection]);*/

                if (limitOrder < sequence.CurrentClosePrice && !isTraining)
                    limitOrder = sequence.CurrentClosePrice;
            }
            else {
                /*double invertedRsi = 100 - sequence.Rsi;
                double invertedMfi = 100 - sequence.Mfi;*/

                limitOrder = sequence.CurrentClosePrice
                    - sequence.CurrentClosePrice * (weights[(int)OptimizingParameters.Offset0]
                    - weights[(int)OptimizingParameters.Offset1] * sequence.SmoothedSlope
                    - weights[(int)OptimizingParameters.Offset2] * Math.Pow(sequence.SmoothedSlope, 2)

                    - (sequence.IndicatorDirection * weights[(int)OptimizingParameters.Rsi0] + Math.Pow(sequence.IndicatorDirection, 2) * weights[(int)OptimizingParameters.Rsi1])
                    - (sequence.IndicatorMagnitude * weights[(int)OptimizingParameters.Mfi0] + Math.Pow(sequence.IndicatorMagnitude, 2) * weights[(int)OptimizingParameters.Mfi1])

                    + sequence.ChangeEMA * weights[(int)OptimizingParameters.ChangeEmaCorrection]
                    - sequence.SmoothedSlope * sequence.SmoothedSlopeRSquared * weights[(int)OptimizingParameters.SlopeRSquaredFitCorrection]);

                /*limitOrder = sequence.CurrentClosePrice
                    - sequence.CurrentClosePrice * (weights[(int)OptimizingParameters.Offset0]
                    - weights[(int)OptimizingParameters.Offset1] * sequence.SmoothedSlope
                    - weights[(int)OptimizingParameters.Offset2] * Math.Pow(sequence.SmoothedSlope, 2)

                    - (invertedRsi * weights[(int)OptimizingParameters.Rsi0] + Math.Pow(invertedRsi, 2) * weights[(int)OptimizingParameters.Rsi1])
                    - (invertedMfi * weights[(int)OptimizingParameters.Mfi0] + Math.Pow(invertedMfi, 2) * weights[(int)OptimizingParameters.Mfi1])

                    + sequence.ChangeEMA * weights[(int)OptimizingParameters.ChangeEmaCorrection]
                    - sequence.SmoothedSlope * sequence.SmoothedSlopeRSquared * weights[(int)OptimizingParameters.SlopeRSquaredFitCorrection]);*/

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

        public static double RSquared(this ReadOnlySpan<double> expected, ReadOnlySpan<double> observed) {
            double yMean = observed.Mean();
            double ssTot = 0;
            double ssRes = 0;

            for (int i = 0; i < observed.Length; i++) {
                ssTot += Math.Pow((observed[i] - yMean), 2);
                ssRes += Math.Pow((observed[i] - expected[i]), 2);
            }

            return 1 - (ssRes / ssTot);
        }

        private static double RSquared(this ReadOnlySpan<double> expected, double[] observed) => expected.RSquared(new ReadOnlySpan<double>(observed));
    }
}
