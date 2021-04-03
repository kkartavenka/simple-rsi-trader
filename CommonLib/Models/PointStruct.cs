namespace CommonLib.Models
{
    public struct PointStruct
    {
        public PointStruct(double min, double max, double val)
        {
            Min = min;
            Max = max;
            Value = val;
        }

        public double Min { get; set; }
        public double Max { get; set; }
        public double Value { get; set; }
    }
}
