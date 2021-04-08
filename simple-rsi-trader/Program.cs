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
        static readonly int _degreeOfParallelism = 1;// Environment.ProcessorCount;

        #region Training and validation

        static readonly int _horizon = 1;
        static readonly int _testSize = 25;
        static readonly int _validationSize = 50;
        static readonly int _randomInitCount = 1000;

        #endregion

        #region Parameters range setup

        static readonly IntRangeStruct _lastRsiSequence = new(0, 5); // A range through RSi through which line should be "drawn", where 0 represents a single day
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

                OptimizerInitClass optimizer = new(
                    testSize: _testSize,
                    validationSize: _validationSize,
                    rsiRange: _rsiRange,
                    stopLossRange: _stopLossRange,
                    takeProfitRange: _takeProfitRange,
                    data: csvReader.Data,
                    horizon: _horizon,
                    lastRsiSequence: _lastRsiSequence,
                    restrictByDate: _restrictByDate,
                    commission: instrument.Commission,
                    roundPoint: csvReader.RoundPoint);

                optimizer.StartOptimization(randomInitCount: _randomInitCount, degreeOfParallelism: _degreeOfParallelism);
            });
        }

    }
}
