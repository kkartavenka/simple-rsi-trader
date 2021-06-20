using CommonLib;
using CommonLib.Models;
using CommonLib.Models.Range;
using simple_trader.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using static CommonLib.Models.DataModel;

namespace simple_trader
{
    class Program
    {
        static readonly int _degreeOfParallelism = 1;/// Environment.ProcessorCount;

        #region Training and validation

        static int _horizon = 1;
        static readonly int _testSize = 50;

        #endregion

        #region Parameters range setup

        static readonly IntRangeStruct _lastRsiSequence = new(4, 30);
        static readonly IntRangeStruct _rsiRange = new(10, 27);

        static readonly DoubleRangeStruct _rsiBuyLimits = new(10, 70);
        static readonly DoubleRangeStruct _rsiSellLimits = new(30, 90);

        #endregion

        #region Data source location and import restrictions

        static readonly string _dirDataPath = @".\..\..\..\..\Data";
        static readonly DateTime _restrictByDate = new(2010, 01, 01);// new(2000, 01, 01);
        static readonly List<SignalModel> _dailyCharts = new()
        {
            new(name: "USDJPY1440.csv", commission: 0.007, stopLossRange: new(0.2, 0.65), takeProfitRange: new(0.8, 1), randomInitPoint: 100000, datasetInfo: new(validationSize: 0.3, testSize: _testSize)),
            new(name: "EURUSD1440.csv", commission: 0.00007, stopLossRange: new(0.2, 0.65), takeProfitRange: new(0.8, 1), randomInitPoint: 100000, datasetInfo: new(validationSize: 0.3, testSize: _testSize)),
            new(name: "GBPUSD1440.csv", commission: 0.00009, stopLossRange: new(0.2, 0.65), takeProfitRange: new(0.8, 1), randomInitPoint: 100000, datasetInfo: new(validationSize: 0.3, testSize: _testSize)),

            new(name: "USDSGD1440.csv", commission: 0.0002, stopLossRange: new(0.2, 0.6), takeProfitRange: new(0.8, 1), randomInitPoint: 200000, datasetInfo: new(validationSize: 0.3, testSize: _testSize)),

            new(name: "EURCHF1440.csv", commission: 0.00025, stopLossRange: new(0.2, 0.75), takeProfitRange: new(0.8, 1), randomInitPoint: 100000, datasetInfo: new(validationSize: 0.3, testSize: _testSize)),

            new(name: "XAUUSD1440.csv", commission: 0.3, stopLossRange: new(0.25, 0.6), takeProfitRange: new(0.75, 1), randomInitPoint: 50000, datasetInfo: new(validationSize: 0.3, testSize: _testSize)),

            new(name: "NATGAS1440.csv", commission: 0.003, stopLossRange: new(0.05, 0.3), takeProfitRange: new(0.5, 1), randomInitPoint: 50000, datasetInfo: new(validationSize: 0.3, testSize: _testSize)),

            new(name: "XPDUSD1440.csv", commission: 4.31, stopLossRange: new(0.1, 0.75), takeProfitRange: new(0.8, 0.899), randomInitPoint: 500000, datasetInfo: new(validationSize: 0.3, testSize: _testSize)),
            //
            //new(name: "XAUUSD60.csv", commission: 0.3, stopLossRange: new(10, 40), takeProfitRange: new(40, 400), randomInitPoint: 100000, datasetInfo: new(validationSize: 0.1, testSize: _testSize))
        };

        #endregion


        static void Main() {
            Console.Write("Horizon: ");
            if (int.TryParse(Console.ReadLine(), out int newHorizon))
                _horizon = newHorizon;

            Console.Write("Use saved models [y/n]: ");
            bool useModel = Console.ReadKey().KeyChar == 'y';

            _dailyCharts.ForEach(instrument => {
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
                    rsiRange: _rsiRange,
                    stopLossRange: instrument.StopLossRange,
                    takeProfitRange: instrument.TakeProfitRange,
                    data: csvReader.Data,
                    horizon: _horizon,
                    lastRsiSequence: _lastRsiSequence,
                    restrictByDate: _restrictByDate,
                    commission: instrument.Commission,
                    instrument: instrument.Name,
                    roundPoint: csvReader.RoundPoint);

                if (!useModel)
                    optimizer.StartOptimization(randomInitCount: instrument.RandomInitPoint, degreeOfParallelism: _degreeOfParallelism);
                else
                    optimizer.Predict();
            });
        }
    }
}
