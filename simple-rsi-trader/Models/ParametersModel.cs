using CommonLib.Models;
using static CommonLib.Enums.Enums;

namespace simple_rsi_trader.Models
{
    public class ParametersModel
    {
        private const int _fixedOffset = 2;

        public ParametersModel(
            int rsiPeriod,
            PointStruct stopLoss, 
            PointStruct takeProfit, 
            PointStruct rsiLimits,
            PointStruct rsquaredCutOff,
            double[] weights, 
            double[] offset,
            int indicatorLastPointSequence, 
            OperationType operation)
        {
            RSquaredCutOff = rsquaredCutOff;
            RsiLimits = rsiLimits;
            RsiPeriod = rsiPeriod;
            StopLoss = stopLoss;
            TakeProfit = takeProfit;
            Weights = weights;
            Offset = offset;
            Operation = operation;
            IndicatorLastPointSequence = indicatorLastPointSequence;

            ParametersCount = _fixedOffset + weights.Length + Offset.Length + 1;
        }

        public int IndicatorLastPointSequence { get; set; }

        public double[] Offset { get; set; }
        public OperationType Operation { get; set; }
        public double[] OptimizableArray { get; set; }
        public int ParametersCount { get; set; }
        public int RsiPeriod { get; set; }
        public PointStruct StopLoss { get; set; }
        public PointStruct TakeProfit { get; set; }
        public PointStruct RsiLimits { get; set; }
        public PointStruct RSquaredCutOff { get; set; }
        public double[] Weights { get; set; }

        public void ToOptimizableArray()
        {
            OptimizableArray = new double[ParametersCount];
            
            OptimizableArray[(int)OptimizingParameters.StopLoss] = StopLoss.Value;
            OptimizableArray[(int)OptimizingParameters.TakeProfit] = TakeProfit.Value;

            OptimizableArray[(int)OptimizingParameters.Weight0] = Weights[0];
            OptimizableArray[(int)OptimizingParameters.Weight1] = Weights[1];

            OptimizableArray[(int)OptimizingParameters.Offset0] = Offset[0];
            OptimizableArray[(int)OptimizingParameters.Offset1] = Offset[1];
            OptimizableArray[(int)OptimizingParameters.Offset2] = Offset[2];

            OptimizableArray[(int)OptimizingParameters.RSquaredCutOff] = RSquaredCutOff.Value;
        }

        public void ToModel(double[] values)
        {
            StopLoss = new PointStruct(range: StopLoss.Range, val: values[(int)OptimizingParameters.StopLoss]);
            TakeProfit = new PointStruct(range: TakeProfit.Range, val: values[(int)OptimizingParameters.TakeProfit]);

            Weights[0] = values[(int)OptimizingParameters.Weight0];
            Weights[1] = values[(int)OptimizingParameters.Weight1];

            Offset[0] = values[(int)OptimizingParameters.Offset0];
            Offset[1] = values[(int)OptimizingParameters.Offset1];
            Offset[2] = values[(int)OptimizingParameters.Offset2];

            RSquaredCutOff = new PointStruct(range: RSquaredCutOff.Range, val: values[(int)OptimizingParameters.RSquaredCutOff]);
        }
    }
}
