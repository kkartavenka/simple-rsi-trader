using CommonLib.Models.Range;

namespace simple_rsi_trader.Models
{
    public class SignalModel
    {
        public SignalModel(string name, double commission, DoubleRangeStruct stopLossRange, DoubleRangeStruct takeProfitRange) {
            Name = name;
            Commission = commission;
            StopLossRange = stopLossRange;
            TakeProfitRange = takeProfitRange;
        }

        // Filename for the data source excluding full path to the file
        public string Name { get; private set; }

        // Commission associated with the operation
        public double Commission { get; private set; }
        public DoubleRangeStruct StopLossRange { get; private set; }
        public DoubleRangeStruct TakeProfitRange { get; private set; }
    }
}
