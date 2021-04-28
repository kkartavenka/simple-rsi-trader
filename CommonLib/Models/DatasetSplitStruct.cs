namespace CommonLib.Models
{
    public struct DatasetSplitStruct
    {
        public DatasetSplitStruct(double validationSize, double preselectSize, int testSize) {
            ValidationSize = validationSize;
            PreselectSize = preselectSize;
            TestSize = testSize;
        }
        public double ValidationSize { get; set; }
        public double PreselectSize { get; set; }
        public int TestSize { get; set; }
    }
}
