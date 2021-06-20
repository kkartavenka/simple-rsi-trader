using CommonLib.Models;
using CommonLib.Models.Range;

namespace simple_rsi_trader.Models
{
    public class SignalModel
    {
        public SignalModel(DatasetSplitStruct datasetInfo, string name, double commission, DoubleRangeStruct stopLossRange, DoubleRangeStruct takeProfitRange, int randomInitPoint) {
            Name = name;
            Commission = commission;
            StopLossRange = stopLossRange;
            TakeProfitRange = takeProfitRange;
            DatasetInfo = datasetInfo;
            RandomInitPoint = randomInitPoint;
        }

        public DatasetSplitStruct DatasetInfo { get; private set; }
        public string Name { get; private set; }
        public double Commission { get; private set; }
        public int RandomInitPoint { get; private set; }
        public DoubleRangeStruct StopLossRange { get; private set; }
        public DoubleRangeStruct TakeProfitRange { get; private set; }

    }
}