using CommonLib.Models.Range;

namespace CommonLib.Models
{
    public class StandardizeStruct
    {
        public StandardizeStruct(double mean, double sd, ScaleStruct scale) {
            Mean = mean;
            Sd = sd;
            Scale = scale;
        }

        public double Mean { get; }
        public double Sd { get; }
        public ScaleStruct Scale { get; }
    }
}
