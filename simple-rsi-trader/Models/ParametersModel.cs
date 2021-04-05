using CommonLib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace simple_rsi_trader.Models
{
    public class ParametersModel
    {
        private const int _fixedOffset = 2;
        private enum OptimizingParameters : int  {StopLoss = 0, TakeProfit = 1};

        public ParametersModel(PointStruct stopLoss, PointStruct takeProfit, double[] weights)
        {
            StopLoss = stopLoss;
            TakeProfit = takeProfit;
            Weights = weights;

            ParametersCount = _fixedOffset + weights.Length;
        }

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
