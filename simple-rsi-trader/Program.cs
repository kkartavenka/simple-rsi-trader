using CommonLib;
using CommonLib.Models;
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

        static int _horizon = 1;
        static readonly int _testSize = 50;
        static readonly int _randomInitCount = 50000;
        static readonly int _useForTest = 5;

        #endregion

        #region Parameters range setup

        static readonly IntRangeStruct _lastRsiSequence = new(1, 5);
        static readonly IntRangeStruct _rsiRange = new(5, 50);

        static readonly DoubleRangeStruct _rsiBuyLimits = new(10, 70);
        static readonly DoubleRangeStruct _rsiSellLimits = new(30, 90);

        #endregion

        #region Data source location and import restrictions

        static readonly string _dirDataPath = @".\..\..\..\..\Data";
        static readonly DateTime _restrictByDate = new(2000, 01, 01);
        static readonly List<SignalModel> _dailyCharts = new()
        {
            new(name: "USDJPY1440.csv", commission: 0.007, stopLossRange: new(10, 40), takeProfitRange: new(80, 300), randomInitPoint: 100000, datasetInfo: new(validationSize: 0.1, preselectSize: 0.5, testSize: _testSize)),
            new(name: "EURUSD1440.csv", commission: 0.00007, stopLossRange: new(10, 80), takeProfitRange: new(80, 500), randomInitPoint: 10000000, datasetInfo: new(validationSize: 0.1, preselectSize: 0.5, testSize: _testSize)),
            new(name: "NATGAS1440.csv", commission: 0.003, stopLossRange: new(10, 30), takeProfitRange: new(30, 500), randomInitPoint: 5000000, datasetInfo: new(validationSize: 0.1, preselectSize: 0.5, testSize: _testSize)),
            new(name: "XPDUSD1440.csv", commission: 4.31, stopLossRange: new(3, 10), takeProfitRange: new(30, 80), randomInitPoint: 10000000, datasetInfo: new(validationSize: 0.1, preselectSize: 0.5, testSize: _testSize)),
            new(name: "XAUUSD1440.csv", commission: 0.3, stopLossRange: new(10, 40), takeProfitRange: new(50, 400), randomInitPoint: 100000, datasetInfo: new(validationSize: 0.1, preselectSize: 0.5, testSize: _testSize)),
        };

        #endregion

        static void Main()
        {
            Console.Write("Horizon: ");
            if (int.TryParse(Console.ReadLine(), out int newHorizon))
                _horizon = newHorizon;

            Console.Write("Use saved models [y/n]: ");
            bool useModel = Console.ReadKey().KeyChar == 'y';

            _dailyCharts.ForEach(instrument =>
            {
                Console.WriteLine($"{Environment.NewLine}{instrument.Name}");

                CsvReaderClass csvReader = new(
                    fileName: Path.Combine(_dirDataPath, instrument.Name),
                    splitChar: ',',
                    restrictDate: _restrictByDate.AddDays(-1.5 * _rsiRange.Max));

                csvReader.PrepareSourceData(
                    dataArraySize: Enum.GetNames(typeof(DataColumn)).Length,
                    cleanUp: true);

                OptimizerInitClass optimizer = new(
                    testSize: instrument.DatasetInfo.TestSize,
                    validationSize: (int)(instrument.DatasetInfo.ValidationSize * (csvReader.DataSize - instrument.DatasetInfo.TestSize)),
                    useForTest: _useForTest,
                    rsiRange: _rsiRange,
                    rsiBuyLimits: _rsiBuyLimits,
                    rsiSellLimits: _rsiSellLimits,
                    stopLossRange: instrument.StopLossRange,
                    takeProfitRange: instrument.TakeProfitRange,
                    data: csvReader.Data,
                    horizon: _horizon,
                    lastRsiSequence: _lastRsiSequence,
                    restrictByDate: _restrictByDate,
                    commission: instrument.Commission,
                    instrument: instrument.Name,
                    saveTop: _saveTopModelCount,
                    roundPoint: csvReader.RoundPoint,
                    preselectSize: instrument.DatasetInfo.PreselectSize);

                if (!useModel)
                    optimizer.StartOptimization(randomInitCount: _randomInitCount, degreeOfParallelism: _degreeOfParallelism);
                else
                    optimizer.Predict();
            });
        }

    }
}
