namespace CommonLib.Models
{
    public struct PredictionStruct
    {
        public PredictionStruct(double limitOrder, double stopLoss, double stopLossDistance, double takeProfit, double takeProfitDistance) {
            LimitOrder = limitOrder;

            StopLoss = stopLoss;
            StopLossDistance = stopLossDistance;

            TakeProfit = takeProfit;
            TakeProfitDistance = takeProfitDistance;
        }

        public double LimitOrder { get; set; }
        public double StopLoss { get; set; }
        public double StopLossDistance { get; set; }
        public double TakeProfit { get; set; }
        public double TakeProfitDistance { get; set; }
    }
}
