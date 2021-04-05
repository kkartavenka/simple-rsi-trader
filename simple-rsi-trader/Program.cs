using CommonLib;
using CommonLib.Models.Range;
using simple_rsi_trader.Models;
using System;
using System.Collections.Generic;
using System.IO;
using static CommonLib.Models.DataModel;

namespace simple_rsi_trader
{
    class Program
    {
        #region Training and validation

        static readonly double _testSize = 25;
        static readonly double _validationSize = 50;

        #endregion

        #region Parameters range setup

        static readonly IntRangeStruct _rsiRange = new(7, 21);
        static readonly DoubleRangeStruct _stopLossRange = new(10, 20); // Definied as a multiplier to commission
        static readonly DoubleRangeStruct _takeProfitRange = new(40, 50); // Definied as a multiplier to commission
        #endregion

        #region Data source location and import restrictions

        static readonly string _dirDataPath = @".\..\..\..\..\Data";
        static readonly DateTime _restrictByDate = new(2000, 01, 01);
        static readonly List<SignalModel> _dailyCharts = new()
        {
            new SignalModel(name: "XAUUSD1440.csv", commission: 0.3)
        };

        #endregion

        static void Main()
        {
            _dailyCharts.ForEach(instrument =>
            {
                Console.WriteLine(instrument.Name);

                CsvReaderClass csvReader = new(
                    fileName: Path.Combine(_dirDataPath, instrument.Name),
                    splitChar: ',',
                    restrictDate: _restrictByDate.AddDays(-1.5 * _rsiRange.Max));

                csvReader.PrepareSourceData(
                    dataArraySize: Enum.GetNames(typeof(DataColumn)).Length,
                    cleanUp: true);
            });
        }

    }
}
