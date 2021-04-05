using CommonLib.Models.Range;

namespace CommonLib.Models
{
    public struct PointStruct
    {
        public PointStruct(DoubleRangeStruct range, double val)
        {
            Range = range;
            Value = val;
        }

        public DoubleRangeStruct Range { get; set; }
        public double Value { get; set; }
    }
}