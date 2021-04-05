using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CommonLib.CsvReaderClass;

namespace CommonLib.Extensions
{
    public static class StringExtensionClass
    {
        public static T ConvertTo<T>(this string[] values, MetaTraderColumn column) => (T)Convert.ChangeType(values[(int)column], typeof(T));
    }
}
