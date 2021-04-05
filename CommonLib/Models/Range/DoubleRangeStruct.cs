namespace CommonLib.Models.Range
{
    public struct DoubleRangeStruct
    {
        public DoubleRangeStruct(double min, double max)
        {
            Max = max;
            Min = min;
        }

        public double Max { get; set; }
        public double Min { get; set; }
    }
}
