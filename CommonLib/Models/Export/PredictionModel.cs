namespace CommonLib.Models.Export
{
    public class PredictionModel
    {
        public PredictionModel(int id, PredictionStruct prediction) {
            Prediction = prediction;
            Id = id;
        }

        public int Id { get; set; }
        public PredictionStruct Prediction { get; set; }

        public string ToSqliteRow() => $"{Id},{Prediction.LimitOrder},{Prediction.StopLoss},{Prediction.TakeProfit}";
    }
}
