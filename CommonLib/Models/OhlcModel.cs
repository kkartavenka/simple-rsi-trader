using System;

namespace CommonLib.Models
{
    public class OhlcModel
    {
        public OhlcModel(double open, double high, double low, double close, double volume, DateTime date)
        {
            High = high;
            Low = low;
            Close = close;
            Volume = volume;
            Date = date;
            TypicalPrice = (high + low + close) / 3;
        }

        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Open { get; set; }
        public double Volume { get; set; }
        public double TypicalPrice { get; set; }
        public DateTime Date { get; set; }

    }
}
