using CommonLib.Classes.DeepCopy;
using CommonLib.Extensions;
using CommonLib.Indicators;
using CommonLib.Models;
using CommonLib.Models.Range;
using simple_rsi_trader.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static CommonLib.Models.DataModel;
using static simple_rsi_trader.Classes.OptimizerClass;
using static simple_rsi_trader.Models.ParametersModel;

namespace simple_rsi_trader.Classes
{
    public class OptimizerInitClass
    {
        private const double _rsiConstantBuy = 50;
        private const double _rsiConstantSell = 50;
        private const double _rsiMin = 10;
        private const double _rsiMax = 90;

        private readonly int _saveTop;

        private readonly double _commission;
        private readonly int _roundPoint;

        private readonly DataModel[] _sourceData;
        private readonly int _testSize;
        private readonly IntRangeStruct _rsiRange;
        private readonly DoubleRangeStruct _stopLossRange;
        private readonly DoubleRangeStruct _takeProfitRange;
        private readonly IntRangeStruct _lastRsiSequence;
        private readonly int _horizon;
        private readonly string _instrument;

        private readonly Thread _saveThread;
        private readonly ConcurrentQueue<SavedModel> _parametersQueue = new();
        private readonly List<SavedModel> _parametersSaved = new();

        private int _modelsLeft;


        private bool _completionToken = false;

        private Dictionary<int, DataModel[]> RsiEnrichedCollection { get; set; } = new Dictionary<int, DataModel[]>();
        private Dictionary<int, SequenceClass[]> TrainSet { get; set; } = new Dictionary<int, SequenceClass[]>();
        private Dictionary<int, SequenceClass[]> TestSet { get; set; } = new Dictionary<int, SequenceClass[]>();

        public OptimizerInitClass(
            int testSize,
            IntRangeStruct rsiRange,
            DoubleRangeStruct stopLossRange,
            DoubleRangeStruct takeProfitRange,
            DataModel[] data,
            IntRangeStruct lastRsiSequence,
            int horizon,
            DateTime restrictByDate,
            double commission,
            string instrument,
            int saveTop,
            int roundPoint) {
            _saveTop = saveTop;
            _instrument = instrument;
            _commission = commission;
            _roundPoint = roundPoint;
            _horizon = horizon;
            _lastRsiSequence = lastRsiSequence;
            _testSize = testSize;
            _rsiRange = rsiRange;
            _stopLossRange = new (min: stopLossRange.Min * _commission, max: stopLossRange.Max * _commission);
            _takeProfitRange = new (min: takeProfitRange.Min * _commission, max: takeProfitRange.Max * _commission);
            _sourceData = data;

            InitializeRsi(restrictByDate);
            CreateSequences();

            _saveThread = new Thread(new ThreadStart(SaveThread));
            _saveThread.Start();
        }

        private void CreateSequences() {
            int testTestEndIndex = RsiEnrichedCollection[_rsiRange.Min].Length - _testSize;

            foreach (KeyValuePair<int, DataModel[]> data in RsiEnrichedCollection) {
                List<SequenceClass> trainSequences = new();
                List<SequenceClass> testSequences = new();

                for (int i = _lastRsiSequence.Max; i < testTestEndIndex; i++)
                    trainSequences.Add(new(before: data.Value[(i - _lastRsiSequence.Max)..i], after: data.Value[i..(i + _horizon)]));

                for (int i = testTestEndIndex; i < data.Value.Length - _horizon; i++)
                    testSequences.Add(new(before: data.Value[(i - _lastRsiSequence.Max)..i], after: data.Value[i..(i + _horizon)]));

                TrainSet.Add(data.Key, trainSequences.ToArray());
                TestSet.Add(data.Key, testSequences.ToArray());
            }
        }

        private void DisplayResults() {
            Console.WriteLine("Optimization done");
            _parametersSaved.GroupBy(m => m.Parameters.Operation).ToList().ForEach(operation => {
                Console.WriteLine(operation.Key);

                IEnumerable<SavedModel> top3 = operation.ToList().OrderByDescending(m => m.TrainedPerformance.Profit).Take(3);

                foreach (SavedModel topModel in top3) {
                    Console.WriteLine($"Operation\t{topModel.Parameters.Operation}");
                    Console.WriteLine($"Profit: {topModel.TrainedPerformance.Profit:N3}\tActions: {topModel.TrainedPerformance.ActionCount}\tWR: {topModel.TrainedPerformance.WinRate}\tLR: {topModel.TrainedPerformance.LossRate}");
                    Console.WriteLine($"Profit: {topModel.TestedPerformance.Profit:N3}\tActions: {topModel.TestedPerformance.ActionCount}\tWR: {topModel.TestedPerformance.WinRate}\tLR: {topModel.TestedPerformance.LossRate}");
                    Console.WriteLine($"Indicator last points: {topModel.Parameters.IndicatorLastPointSequence}");
                    Console.WriteLine($"Limit order offset: {topModel.Parameters.Offset[0]}\t{topModel.Parameters.Offset[1]}");
                    Console.WriteLine($"RSi line weights: {topModel.Parameters.Weights[0]}\t{topModel.Parameters.Weights[1]}");
                    Console.WriteLine($"RSi period: {topModel.Parameters.RsiPeriod}");
                    Console.WriteLine($"Stop loss: {topModel.Parameters.StopLoss.Value}");
                    Console.WriteLine($"Take profit: {topModel.Parameters.TakeProfit.Value}");
                    Console.WriteLine();
                }
            });
        }

        private List<ParametersModel> GenerateInitPoints(int count) {
            List<ParametersModel> returnVar = new();

            for (int i = 0; i < count; i++) {
                double buyConstant = _rsiMin + (_rsiConstantBuy - _rsiMin).GetRandomDouble();
                double sellConstant = _rsiConstantSell + (_rsiMax - _rsiConstantSell).GetRandomDouble();

                int lastPointSequence = (_lastRsiSequence.Max - _lastRsiSequence.Min).GetRandomInt() + _lastRsiSequence.Min;

                double buySlope = lastPointSequence == 0 ? 0 : ((buyConstant - _rsiMin) / lastPointSequence).GetRandomDouble();
                double sellSlope = lastPointSequence == 0 ? 0 : ((_rsiMax - sellConstant) / lastPointSequence).GetRandomDouble();

                returnVar.Add(new(
                    rsiPeriod: (_rsiRange.Max - _rsiRange.Min).GetRandomInt() + _rsiRange.Min,
                    stopLoss: new(_stopLossRange, (_stopLossRange.Max - _stopLossRange.Min).GetRandomDouble() + _stopLossRange.Min),
                    takeProfit: new(_takeProfitRange, (_takeProfitRange.Max - _takeProfitRange.Min).GetRandomDouble() + _takeProfitRange.Min),
                    weights: new double[] { buyConstant, buySlope },
                    indicatorLastPointSequence: lastPointSequence,
                    offset: new double[] { 10d.GetRandomDouble(), 0.1d.GetRandomDouble() },
                    operation: OperationType.Buy));

                returnVar.Add(new(
                    rsiPeriod: (_rsiRange.Max - _rsiRange.Min).GetRandomInt() + _rsiRange.Min,
                    stopLoss: new(_stopLossRange, (_stopLossRange.Max - _stopLossRange.Min).GetRandomDouble() + _stopLossRange.Min),
                    takeProfit: new(_takeProfitRange, (_takeProfitRange.Max - _takeProfitRange.Min).GetRandomDouble() + _takeProfitRange.Min),
                    weights: new double[] { sellConstant, sellSlope },
                    indicatorLastPointSequence: lastPointSequence,
                    offset: new double[] { 10d.GetRandomDouble(), 0.1d.GetRandomDouble() },
                    operation: OperationType.Sell));
            }

            _modelsLeft = returnVar.Count;
            Console.WriteLine($"Models created for evaluation: {_modelsLeft}");

            return returnVar;
        }

        private void InitializeRsi(DateTime restrictByDate) {
            for (int i = _rsiRange.Min; i < _rsiRange.Max; i++) {
                RsiClass rsi = new(period: i);
                DataModel[] data = _sourceData.Copy();

                data = rsi.GetRSi(data, (int)DataColumn.Close, (int)DataColumn.Rsi);
                data = data
                    .Where(m => m.Date >= restrictByDate)
                    .OrderBy(m => m.Date)
                    .ToArray();

                RsiEnrichedCollection.Add(i, data);
            }
        }

        private void SaveThread() {
            while (!_completionToken) {
                while (_parametersQueue.TryDequeue(out SavedModel newModel))
                    _parametersSaved.Add(newModel);

                List<SavedModel> savedFiltered = new ();
                _parametersSaved.GroupBy(m => m.Parameters.Operation).ToList().ForEach(row => savedFiltered.AddRange(row.OrderByDescending(m => m.TrainedPerformance.Profit).Take(_saveTop)));

                Console.WriteLine($"{DateTime.Now} Models left: {_modelsLeft}");

                try {
                    File.WriteAllText($"{_instrument}.trained", JsonSerializer.Serialize(_parametersSaved));
                }
                catch (Exception exception) {
                    Console.WriteLine(exception.Message);
                }

                Thread.Sleep(5000);
            }
        }

        public void StartOptimization(int randomInitCount, int degreeOfParallelism = -1) {
            int degOfParal = degreeOfParallelism == -1 ? Environment.ProcessorCount : degreeOfParallelism;
            List<ParametersModel> initParameters = GenerateInitPoints(randomInitCount);

            initParameters.AsParallel().WithDegreeOfParallelism(degOfParal).ForAll(parameter => {
                OptimizerClass optimizer = new (sequences: TrainSet[parameter.RsiPeriod], parameter: parameter, commission: _commission, roundPoint: _roundPoint);
                optimizer.Optimize();

                if (optimizer.IsSuccess) {
                    optimizer.LoadSequence(TestSet[parameter.RsiPeriod]);
                    optimizer.Test();

                    if (optimizer.IsSuccess)
                        _parametersQueue.Enqueue(new (parameters: parameter.Copy(), tested: optimizer.Performance[ExecutionType.Test], trained: optimizer.Performance[ExecutionType.Train]));
                }

                _modelsLeft--;
            });

            _completionToken = true;
            _saveThread.Join();

            DisplayResults();
        }
    }
}