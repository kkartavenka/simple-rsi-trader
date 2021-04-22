using System;

namespace CommonLib.Models
{
    public class DataModel
    {
        public enum DataColumn : int { 
            Open = 0, 
            High = 1, 
            Low = 2, 
            Close = 3, 
            TypicalPrice = 4, 
            Volume = 5,

            Rsi = 6
        };

        public DataModel(double[] data, DateTime date, int id)
        {
            Id = id;
            Data = data;
            Date = date;
        }
        public int Id { get; private set; }
        public DateTime Date { get; private set; }
        public double[] Data { get; set; }

        public string ToSqliteRow() => $"{Id},{Date:yyyy-MM-dd HH:mm:ss},{Data[(int)DataColumn.Open]},{Data[(int)DataColumn.High]},{Data[(int)DataColumn.Low]},{Data[(int)DataColumn.Close]},{Data[(int)DataColumn.TypicalPrice]},{Data[(int)DataColumn.Volume]}";
    }
}
