namespace CommonLib.Models.Range
{
    public struct IntRangeStruct
    {
        public IntRangeStruct(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public int Min { get; set; }
        public int Max { get; set; }
    }
}
