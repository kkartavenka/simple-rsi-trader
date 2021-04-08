using CommonLib.Models;

namespace simple_rsi_trader.Models
{
    public class ParametersModel
    {
        private const int _fixedOffset = 2;
        public enum OptimizingParameters : int  {StopLoss = 0, TakeProfit = 1, Weight0 = 2, Weight1 = 3, Offset0 = 4, Offset1 = 5 };
        public enum OperationType : int { Buy = 0, Sell = 1 };

        public ParametersModel(
            int rsiPeriod,
            PointStruct stopLoss, 
            PointStruct takeProfit, 
            double[] weights, 
            double[] offset,
            int indicatorLastPointSequence, 
            OperationType operation)
        {
            RsiPeriod = rsiPeriod;
            StopLoss = stopLoss;
            TakeProfit = takeProfit;
            Weights = weights;
            Offset = offset;
            Operation = operation;
            IndicatorLastPointSequence = indicatorLastPointSequence;

            ParametersCount = _fixedOffset + weights.Length + Offset.Length;
        }

        public int IndicatorLastPointSequence { get; private set; }

        public double[] Offset { get; private set; }
        public OperationType Operation { get; private set; }
        public double[] OptimizableArray { get; set; }
        public int ParametersCount { get; private set; }
        public int RsiPeriod { get; private set; }
        public PointStruct StopLoss { get; private set; }

        public PointStruct TakeProfit { get; private set; }

        public double[] Weights { get; private set; }

        public void ToOptimizableArray()
        {
            OptimizableArray = new double[ParametersCount];
            
            OptimizableArray[(int)OptimizingParameters.StopLoss] = StopLoss.Value;
            OptimizableArray[(int)OptimizingParameters.TakeProfit] = TakeProfit.Value;

            OptimizableArray[(int)OptimizingParameters.Weight0] = Weights[0];
            OptimizableArray[(int)OptimizingParameters.Weight1] = Weights[1];

            OptimizableArray[(int)OptimizingParameters.Offset0] = Offset[0];
            OptimizableArray[(int)OptimizingParameters.Offset1] = Offset[1];
        }

        public void ToModel(double[] values)
        {
            StopLoss = new PointStruct(range: StopLoss.Range, val: values[(int)OptimizingParameters.StopLoss]);
            TakeProfit = new PointStruct(range: TakeProfit.Range, val: values[(int)OptimizingParameters.TakeProfit]);

            Weights[0] = values[(int)OptimizingParameters.Weight0];
            Weights[1] = values[(int)OptimizingParameters.Weight1];

            Offset[0] = values[(int)OptimizingParameters.Offset0];
            Offset[1] = values[(int)OptimizingParameters.Offset1];
        }
    }
}
