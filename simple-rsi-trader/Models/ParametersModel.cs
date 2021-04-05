using CommonLib.Models;

namespace simple_rsi_trader.Models
{
    public class ParametersModel
    {
        private const int _fixedOffset = 2;
        public enum OptimizingParameters : int  {StopLoss = 0, TakeProfit = 1};
        public enum OperationType : int { Buy = 0, Sell = 1 };

        public ParametersModel(PointStruct stopLoss, PointStruct takeProfit, double[] weights, int indicatorLastPointSequence, OperationType operation)
        {
            StopLoss = stopLoss;
            TakeProfit = takeProfit;
            Weights = weights;
            Operation = operation;
            IndicatorLastPointSequence = indicatorLastPointSequence;

            ParametersCount = _fixedOffset + weights.Length;
        }

        public int IndicatorLastPointSequence { get; private set; }

        public OperationType Operation { get; private set; }
        public double[] OptimizableArray { get; set; }
        public int ParametersCount { get; private set; }

        public PointStruct StopLoss { get; private set; }

        public PointStruct TakeProfit { get; private set; }

        public double[] Weights { get; private set; }

        public void ToOptimizableArray()
        {
            OptimizableArray = new double[ParametersCount];
            OptimizableArray[(int)OptimizingParameters.StopLoss] = StopLoss.Value;
            OptimizableArray[(int)OptimizingParameters.TakeProfit] = TakeProfit.Value;
            for (int i = _fixedOffset; i < OptimizableArray.Length; i++)
                OptimizableArray[i] = Weights[i - _fixedOffset];
        }

        public void ToModel()
        {
            StopLoss = new PointStruct(range: StopLoss.Range, val: OptimizableArray[(int)OptimizingParameters.StopLoss]);
            TakeProfit = new PointStruct(range: TakeProfit.Range, val: OptimizableArray[(int)OptimizingParameters.TakeProfit]);

            for (int i = _fixedOffset; i < OptimizableArray.Length; i++)
                Weights[i] = OptimizableArray[i - _fixedOffset];
        }
    }
}
