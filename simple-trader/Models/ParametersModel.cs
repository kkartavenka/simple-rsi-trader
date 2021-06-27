using CommonLib.Enums;
using CommonLib.Models;
using simple_trader.Enums;

namespace simple_trader.Models
{
    public class ParametersModel
    {
        public ParametersModel(
            int sequenceLength,
            PointStruct stopLoss, 
            PointStruct takeProfit, 

            PointStruct slopeLimits,
            PointStruct slopeLimitsRSquared,
            
            double[] offset,
            OperationType operation,
            double standardDeviationCorrection,

            double[] mfi,
            double[] rsi,
            double slopeRSquaredFitCorrection)
        {            
            SlopeLimits = slopeLimits;
            SlopeLimitsRSquared = slopeLimitsRSquared;
            SequenceLength = sequenceLength;
            StopLoss = stopLoss;
            TakeProfit = takeProfit;
            Offset = offset;
            Operation = operation;
            StandardDeviationCorrection = standardDeviationCorrection;

            Mfi = mfi;
            Rsi = rsi;

            SlopeRSquaredFitCorrection = slopeRSquaredFitCorrection;

            ParametersCount = 13;
        }

        public double[] Offset { get; set; }
        public OperationType Operation { get; set; }
        public double[] OptimizableArray { get; set; }
        public int ParametersCount { get; private set; }
        public int SequenceLength { get; set; }
        public PointStruct StopLoss { get; set; }
        public PointStruct TakeProfit { get; set; }
        public PointStruct SlopeLimits { get; set; }
        public PointStruct SlopeLimitsRSquared { get; set; }
        public double[] Mfi { get; set; }
        public double StandardDeviationCorrection { get; set; }
        public double[] Rsi { get; set; }
        public double SlopeRSquaredFitCorrection { get; set; }

        public void ToOptimizableArray()
        {
            OptimizableArray = new double[ParametersCount];
            
            OptimizableArray[(int)OptimizingParameters.StopLoss] = StopLoss.Value; // 1
            OptimizableArray[(int)OptimizingParameters.TakeProfit] = TakeProfit.Value; // 2

            OptimizableArray[(int)OptimizingParameters.Slope] = SlopeLimits.Value; // 3
            OptimizableArray[(int)OptimizingParameters.RSquared] = SlopeLimitsRSquared.Value; // 3

            OptimizableArray[(int)OptimizingParameters.Offset0] = Offset[0]; // 5
            OptimizableArray[(int)OptimizingParameters.Offset1] = Offset[1]; // 6
            OptimizableArray[(int)OptimizingParameters.Offset2] = Offset[2]; // 7

            OptimizableArray[(int)OptimizingParameters.Mfi0] = Mfi[0]; // 8
            OptimizableArray[(int)OptimizingParameters.Mfi1] = Mfi[1]; // 9
            //OptimizableArray[(int)OptimizingParameters.Mfi2] = Mfi[2]; // 10

            OptimizableArray[(int)OptimizingParameters.Rsi0] = Rsi[0]; // 11
            OptimizableArray[(int)OptimizingParameters.Rsi1] = Rsi[1]; // 12
            //OptimizableArray[(int)OptimizingParameters.Rsi2] = Rsi[2]; // 13

            OptimizableArray[(int)OptimizingParameters.ChangeEmaCorrection] = StandardDeviationCorrection; // 14
            OptimizableArray[(int)OptimizingParameters.SlopeRSquaredFitCorrection] = SlopeRSquaredFitCorrection; // 15

        }

        public void ToModel(double[] values)
        {
            StopLoss = new (range: StopLoss.Range, val: values[(int)OptimizingParameters.StopLoss]);
            TakeProfit = new (range: TakeProfit.Range, val: values[(int)OptimizingParameters.TakeProfit]);

            SlopeLimits = new (SlopeLimits.Range, val: values[(int)OptimizingParameters.Slope]);
            SlopeLimitsRSquared = new(range: SlopeLimitsRSquared.Range, val: values[(int)OptimizingParameters.RSquared]);

            Offset[0] = values[(int)OptimizingParameters.Offset0];
            Offset[1] = values[(int)OptimizingParameters.Offset1];
            Offset[2] = values[(int)OptimizingParameters.Offset2];

            Mfi[0] = values[(int)OptimizingParameters.Mfi0];
            Mfi[1] = values[(int)OptimizingParameters.Mfi1];
            //Mfi[2] = values[(int)OptimizingParameters.Mfi2];

            Rsi[0] = OptimizableArray[(int)OptimizingParameters.Rsi0];
            Rsi[1] = OptimizableArray[(int)OptimizingParameters.Rsi1];
            //Rsi[2] = OptimizableArray[(int)OptimizingParameters.Rsi2];

            StandardDeviationCorrection = OptimizableArray[(int)OptimizingParameters.ChangeEmaCorrection];
            SlopeRSquaredFitCorrection = OptimizableArray[(int)OptimizingParameters.SlopeRSquaredFitCorrection];

        }
    }
}
