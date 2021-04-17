using CommonLib.Extensions;
using CommonLib.Models.Range;

namespace CommonLib.Models
{
    public class PerformanceModel
    {
        public int ActionCount { get; set; } = 0;

        public double LossCount { get; set; } = 0;
        public double LossRate { get; set; } = 0;

        public double Profit { get; set; } = 0;

        public double Score { get; set; } = 0;

        public double WinRate { get; set; } = 0;
        public int WinCount { get; set; } = 0;


        private readonly DoubleRangeStruct _observed = new(min: 0, max: 1);
        private readonly DoubleRangeStruct _scale = new(min: 0.1, max: 1.5);

        public void CalculateMetrics(double commission, int size) {
            WinRate = (double)WinCount / ActionCount;
            LossRate = (double)LossCount / ActionCount;
            double lossRateScaled = LossRate.ScaleMinMax(_observed, _scale);
            Score = Profit;// + Profit * WinRate / lossRateScaled;

            Profit /= (commission * size);
        }

    }
}
