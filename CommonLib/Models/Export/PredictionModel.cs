using static CommonLib.Enums.Enums;

namespace CommonLib.Models.Export
{
    public class PredictionModel
    {
        public PredictionModel(int id, OperationType operation, PredictionStruct prediction) {
            Prediction = prediction;
            Id = id;
            Operation = operation;
        }

        public int Id { get; private set; }
        public OperationType Operation { get; private set; }
        public PredictionStruct Prediction { get; private set; }

        public string ToSqliteRow() => $"'{Operation}',{Id},{Prediction.LimitOrder},{Prediction.StopLoss},{Prediction.TakeProfit}";
    }
}
