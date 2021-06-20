using CommonLib.Enums;
using CommonLib.Models;
using simple_trader.Enums;

namespace simple_trader.Models
{
    public class ParametersModel
    {
        private const int _fixedOffset = 2;

        public ParametersModel(
            int sequenceLength,
            PointStruct stopLoss, 
            PointStruct takeProfit, 

            PointStruct slopeLimits,
            PointStruct slopeLimitsRSquared,
            
            PointStruct rsquaredCutOff,
            double[] offset,
            OperationType operation,
            double standardDeviationCorrection,
            double rsiSlopeFitCorrection)
        {
            RSquaredCutOff = rsquaredCutOff;
            
            SlopeLimits = slopeLimits;
            SlopeLimitsRSquared = slopeLimitsRSquared;

            SequenceLength = sequenceLength;
            StopLoss = stopLoss;
            TakeProfit = takeProfit;
            Offset = offset;
            Operation = operation;
            StandardDeviationCorrection = standardDeviationCorrection;
            RsiSlopeFitCorrection = rsiSlopeFitCorrection;

            ParametersCount = _fixedOffset + Offset.Length + 5;
        }

        public double[] Offset { get; set; }
        public OperationType Operation { get; set; }
        public double[] OptimizableArray { get; set; }
        public int ParametersCount { get; set; }
        public int SequenceLength { get; set; }
        public PointStruct StopLoss { get; set; }
        public PointStruct TakeProfit { get; set; }
        public PointStruct SlopeLimits { get; set; }
        public PointStruct SlopeLimitsRSquared { get; set; }
        public PointStruct RSquaredCutOff { get; set; }
        public double StandardDeviationCorrection { get; set; }
        public double RsiSlopeFitCorrection { get; set; }

        public void ToOptimizableArray()
        {
            OptimizableArray = new double[ParametersCount];
            
            OptimizableArray[(int)OptimizingParameters.StopLoss] = StopLoss.Value;
            OptimizableArray[(int)OptimizingParameters.TakeProfit] = TakeProfit.Value;

            OptimizableArray[(int)OptimizingParameters.Slope] = SlopeLimits.Value;
            OptimizableArray[(int)OptimizingParameters.RSquared] = SlopeLimitsRSquared.Value;

            OptimizableArray[(int)OptimizingParameters.Offset0] = Offset[0];
            OptimizableArray[(int)OptimizingParameters.Offset1] = Offset[1];
            OptimizableArray[(int)OptimizingParameters.Offset2] = Offset[2];
            OptimizableArray[(int)OptimizingParameters.Offset3] = Offset[3];

            OptimizableArray[(int)OptimizingParameters.RSquaredCutOff] = RSquaredCutOff.Value;

            OptimizableArray[(int)OptimizingParameters.ChangeEmaCorrection] = StandardDeviationCorrection;
            OptimizableArray[(int)OptimizingParameters.RsiSlopeFitCorrection] = RsiSlopeFitCorrection;

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
            Offset[3] = values[(int)OptimizingParameters.Offset3];

            RSquaredCutOff = new (range: RSquaredCutOff.Range, val: values[(int)OptimizingParameters.RSquaredCutOff]);

            StandardDeviationCorrection = OptimizableArray[(int)OptimizingParameters.ChangeEmaCorrection];
            RsiSlopeFitCorrection = OptimizableArray[(int)OptimizingParameters.RsiSlopeFitCorrection];

        }
    }
}
