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
    }
}
