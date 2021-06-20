namespace CommonLib.Models.Range
{
    public struct ScaleStruct
    {
        public ScaleStruct(DoubleRangeStruct scaleFrom, DoubleRangeStruct scaleTo) {
            ScaleTo = scaleTo;
            ScaleFrom = scaleFrom;
        }

        public DoubleRangeStruct ScaleFrom { get; }
        public DoubleRangeStruct ScaleTo { get; }
    }
}
