namespace CommonLib.Models
{
    public struct PredictionStruct
    {
        public PredictionStruct(double limitOrder, double stopLoss, double takeProfit) {
            LimitOrder = limitOrder;
            StopLoss = stopLoss;
            TakeProfit = takeProfit;
        }

        public double LimitOrder { get; set; }
        public double StopLoss { get; set; }
        public double TakeProfit { get; set; }
    }
}
