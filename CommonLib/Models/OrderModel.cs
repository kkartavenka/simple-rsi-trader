namespace CommonLib.Models
{
    public class OrderModel
    {
        public OrderModel(double close, double order, double low, double high, double lowestPrice, double highestPrice) {
            Close = close;
            Order = order;
            High = high;
            Low = low;
            LowestPrice = lowestPrice;
            HighestPrice = highestPrice;
        }

        public double Close { get; set; }

        public double High { get; set; }
        public double HighestPrice { get; set; }

        public double Low { get; set; }
        public double LowestPrice { get; set; }

        public double Order { get; set; }
    }
}
