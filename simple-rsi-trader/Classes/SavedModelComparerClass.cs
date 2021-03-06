using simple_rsi_trader.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace simple_rsi_trader.Classes
{
    public class SavedModelComparerClass : IEqualityComparer<SavedModel>
    {
        public bool Equals(SavedModel x, SavedModel y) {
            bool trainProfit = Math.Max(x.TrainedPerformance.Profit, y.TrainedPerformance.Profit) / Math.Min(x.TrainedPerformance.Profit, y.TrainedPerformance.Profit) < 0.01;
            bool testProfit = Math.Max(x.TestedPerformance.Profit, y.TestedPerformance.Profit) / Math.Min(x.TestedPerformance.Profit, y.TestedPerformance.Profit) < 0.01;



            bool offset = true;
            for (int i = 0; i < x.Parameters.Offset.Length; i++)
                offset &= x.Parameters.Offset[i] == y.Parameters.Offset[i];

            bool weights = true;
            for (int i = 0; i < x.Parameters.Weights.Length; i++)
                weights &= x.Parameters.Weights[i] == y.Parameters.Weights[i];

            return weights && offset && testProfit && trainProfit;
        }

        public int GetHashCode([DisallowNull] SavedModel obj) {
            int profitHash = obj.TestedPerformance.Profit.GetHashCode() ^ obj.TrainedPerformance.Profit.GetHashCode();
            int rsiHash = obj.Parameters.RsiPeriod.GetHashCode();
            int stopLossHash = obj.Parameters.StopLoss.GetHashCode();
            int takeProfitHash = obj.Parameters.TakeProfit.GetHashCode();

            return profitHash ^ rsiHash ^ takeProfitHash ^ stopLossHash;
        }
    }
}
