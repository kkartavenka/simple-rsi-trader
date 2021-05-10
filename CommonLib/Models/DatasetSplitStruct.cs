namespace CommonLib.Models
{
    public struct DatasetSplitStruct
    {
        public DatasetSplitStruct(double validationSize, int testSize) {
            ValidationSize = validationSize;
            TestSize = testSize;
        }
        public double ValidationSize { get; set; }
        public int TestSize { get; set; }
    }
}
