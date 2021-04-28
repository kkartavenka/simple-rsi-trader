namespace CommonLib.Models
{
    public class OrderModel
    {
        public OrderModel(double endPeriodClosePrice, double order, double firstLow, double firstHigh, double lowestPrice, double highestPrice, double nonFirstHighestPrice, double nonFirstLowestPrice) {
            EndPeriodClosePrice = endPeriodClosePrice;
            Order = order;
            FirstHigh = firstHigh;
            FirstLow = firstLow;
            LowestPrice = lowestPrice;
            HighestPrice = highestPrice;

            NonFirstHighestPrice = nonFirstHighestPrice;
            NonFirstLowestPrice = nonFirstLowestPrice;
        }

        public double EndPeriodClosePrice { get; set; }

        public double FirstHigh { get; set; }
        public double HighestPrice { get; set; }
        public double NonFirstHighestPrice { get; set; }

        public double FirstLow { get; set; }
        public double LowestPrice { get; set; }
        public double NonFirstLowestPrice { get; set; }

        public double Order { get; set; }
    }
}
