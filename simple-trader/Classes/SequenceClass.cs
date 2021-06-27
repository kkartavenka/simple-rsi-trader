using Accord.Statistics;
using Accord.Statistics.Kernels;
using Accord.Statistics.Models.Regression.Linear;
using CommonLib.Models;
using System;
using System.Linq;
using static CommonLib.Models.DataModel;

#nullable enable
namespace simple_trader.Classes
{
    public class SequenceClass
    {
        // Allow maximum 10% change over period
        private const double _maxAbsoluteChange = 0.1;

        public SequenceClass(DataModel[] before, DataModel[]? after, int priceSequenceLimit) {
            var changes = new double[before.Length];
            for (int i = 1; i < before.Length; i++)
                changes[i - 1] = Math.Abs(before[i].Data[(int)DataColumn.Close] - before[i - 1].Data[(int)DataColumn.Close]);

            ChangeEMA = Measures.ExponentialWeightedMean(changes, 0.2);

            CurrentClosePrice = before[^1].Data[(int)DataColumn.Close];
            ClosePriceSlope = before[^1].Data[(int)DataColumn.ClosePriceSlope];
            ClosePriceSlopeRSquared = before[^1].Data[(int)DataColumn.ClosePriceSlopeRSquared];

            if (after != null) {
                EndPeriodClosePrice = after[^1].Data[(int)DataColumn.Close];

                FirstPeriodHighPrice = after[0].Data[(int)DataColumn.High];
                HighestPrice = after.Select(m => m.Data[(int)DataColumn.High]).Max();
                double highChange = (HighestPrice - CurrentClosePrice) / CurrentClosePrice / after.Length;

                FirstPeriodLowPrice = after[0].Data[(int)DataColumn.Low];
                LowestPrice = after.Select(m => m.Data[(int)DataColumn.Low]).Min();
                double lowChange = (LowestPrice - CurrentClosePrice) / CurrentClosePrice / after.Length;

                if (after.Length > 1) {
                    NonFirstHighestPrice = after[1..].Select(m => m.Data[(int)DataColumn.High]).Max();
                    NonFirstLowestPrice = after[1..].Select(m => m.Data[(int)DataColumn.Low]).Min();
                }

                if (highChange < 0 || highChange > _maxAbsoluteChange)
                    AllowSell = false;

                if (lowChange > 0 || lowChange < (-1) * _maxAbsoluteChange)
                    AllowBuy = false;
            }


            Id = before[^1].Id;
            Date = before[^1].Date;

            Rsi = before[^1].Data[(int)DataColumn.Rsi];
            Mfi = before[^1].Data[(int)DataColumn.Mfi];

            double[] closePrices = before.Select(m => m.Data[(int)DataColumn.Close]).ToArray();
            int windowSize = 3;
            double[] x = new double[closePrices.Length - windowSize + 1];
            double[] y = new double[closePrices.Length - windowSize + 1];
            for (int i = windowSize; i < closePrices.Length + 1; i++) {
                x[i - windowSize] = i - windowSize;
                y[i - windowSize] = closePrices[(i - windowSize)..(i)].Mean();
            }

            x = x[^(priceSequenceLimit - windowSize)..];
            y = y[^(priceSequenceLimit - windowSize)..];

            OrdinaryLeastSquares ols = new();
            SimpleLinearRegression slr = ols.Learn(x, y);

            SmoothedSlope = slr.Slope;
            ReadOnlySpan<double> expectedY = new ReadOnlySpan<double>(slr.Transform(x));
            SmoothedSlopeRSquared = expectedY.RSquared(y);

            IndicatorDirection = Rsi != 0 ? Mfi / Rsi : 0;
            IndicatorMagnitude = Math.Sqrt(Mfi * Mfi + Rsi * Rsi);
        }

        public bool AllowBuy { get; private set; } = true;
        public bool AllowSell { get; private set; } = true;

        public int Id { get; private set; }

        public double CurrentClosePrice { get; private set; }
        public double EndPeriodClosePrice { get; private set; }
        public double ChangeEMA { get; private set; } 


        public DateTime Date { get; private set; }

        public double HighestPrice { get; private set; }
        public double NonFirstHighestPrice { get; private set; }
        public double FirstPeriodHighPrice { get; private set; }


        public double FirstPeriodLowPrice { get; private set; }
        public double NonFirstLowestPrice { get; private set; }
        public double LowestPrice { get; private set; }

        public double Rsi { get; private set; }
        public double Mfi { get; private set; }
        public double ClosePriceSlope { get; private set; }
        public double ClosePriceSlopeRSquared { get; private set; }

        public double SmoothedSlope { get; private set; }
        public double SmoothedSlopeRSquared { get; private set; }

        public double IndicatorDirection { get; private set; }
        public double IndicatorMagnitude { get; private set; }
    }
}
