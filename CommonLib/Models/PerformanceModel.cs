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

        public void CalculateMetrics(double commission, int size) {
            WinRate = (double)WinCount / ActionCount;
            LossRate = (double)LossCount / ActionCount;

            Profit /= (commission * size);
            Score = Profit; // For now but should be changed in the future
        }

    }
}
