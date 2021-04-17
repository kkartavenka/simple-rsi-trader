using CommonLib;
using CommonLib.Models.Range;
using simple_rsi_trader.Classes;
using simple_rsi_trader.Models;
using System;
using System.Collections.Generic;
using System.IO;
using static CommonLib.Models.DataModel;

namespace simple_rsi_trader
{
    class Program
    {
        static readonly int _degreeOfParallelism = Environment.ProcessorCount;
        static readonly int _saveTopModelCount = 50;

        #region Training and validation

        static readonly int _horizon = 1;
        static readonly int _testSize = 50;
        static readonly int _randomInitCount = 100000;

        #endregion

        #region Parameters range setup

        static readonly IntRangeStruct _lastRsiSequence = new(1, 5);
        static readonly IntRangeStruct _rsiRange = new(7, 21);
        static readonly DoubleRangeStruct _stopLossRange = new(10, 50); // Definied as a multiplier to commission
        static readonly DoubleRangeStruct _takeProfitRange = new(40, 240); // Definied as a multiplier to commission

        static readonly DoubleRangeStruct _rsiBuyLimits = new(10, 60);
        static readonly DoubleRangeStruct _rsiSellLimits = new(40, 90);

        #endregion

        #region Data source location and import restrictions

        static readonly string _dirDataPath = @".\..\..\..\..\Data";
        static readonly DateTime _restrictByDate = new(2000, 01, 01);
        static readonly List<SignalModel> _dailyCharts = new()
        {
            new SignalModel(name: "XAUUSD1440.csv", commission: 0.3),
            //new SignalModel(name: "USDJPY1440.csv", commission: 0.007),
            //new SignalModel(name: "EURUSD1440.csv", commission: 0.00007)
        };

        #endregion

        static void Main()
        {
            Console.Write("Use saved models [y/n]: "); 

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

                OptimizerInitClass optimizer = new(
                    testSize: _testSize,
                    rsiRange: _rsiRange,
                    rsiBuyLimits: _rsiBuyLimits,
                    rsiSellLimits: _rsiSellLimits,
                    stopLossRange: _stopLossRange,
                    takeProfitRange: _takeProfitRange,
                    data: csvReader.Data,
                    horizon: _horizon,
                    lastRsiSequence: _lastRsiSequence,
                    restrictByDate: _restrictByDate,
                    commission: instrument.Commission,
                    instrument: instrument.Name,
                    saveTop: _saveTopModelCount,
                    roundPoint: csvReader.RoundPoint);

                optimizer.StartOptimization(randomInitCount: _randomInitCount, degreeOfParallelism: _degreeOfParallelism);
            });
        }

    }
}
