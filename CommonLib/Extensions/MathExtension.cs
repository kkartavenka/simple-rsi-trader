using System;

namespace CommonLib.Extensions
{
    public static class MathExtension
    {
        public static double Mean(this ReadOnlySpan<double> values) {
            if (values.Length == 0)
                return 0;

            double sum = 0;
            for (int i = 0; i < values.Length; i++)
                sum += values[i];

            return sum / values.Length;
        }
    }
}
