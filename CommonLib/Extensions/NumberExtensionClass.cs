using CommonLib.Models.Range;
using System;
using System.Security.Cryptography;

namespace CommonLib.Extensions
{
    public static class NumberExtensionClass
    {
        public static double GetRandomDouble(this double x)
        {
            var rng = new RNGCryptoServiceProvider();
            var bytes = new byte[8];
            rng.GetBytes(bytes);
            var ul = BitConverter.ToUInt64(bytes, 0) / (1 << 11);
            double returnModel = ul / (double)(1UL << 53);

            return returnModel * x;
        }

        public static int GetRandomInt(this int x) => (int)((double)x).GetRandomDouble();

        public static double ScaleMinMax(this double x, DoubleRangeStruct from, DoubleRangeStruct to) =>
            to.Min + (x - from.Min) * (to.Max - to.Min) / (from.Max - from.Min);

        public static double Standardize(this double x, double sd, double mean) => (x - mean) / sd;
        public static double DeStandardize(this double x, double sd, double mean) => x * sd + mean;
    }
}
