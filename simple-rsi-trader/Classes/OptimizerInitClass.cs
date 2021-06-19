using Accord.Math.Distances;
using Accord.Math.Optimization;
using Accord.Statistics;
using Accord.Statistics.Models.Regression.Linear;
using CommonLib.Classes;
using CommonLib.Classes.DeepCopy;
using CommonLib.Extensions;
using CommonLib.Indicators;
using CommonLib.Models;
using CommonLib.Models.Export;
using CommonLib.Models.Range;
using Newtonsoft.Json;
using simple_rsi_trader.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using static CommonLib.Enums.Enums;
using static CommonLib.Models.DataModel;
using static simple_rsi_trader.Classes.OptimizerClass;

namespace simple_rsi_trader.Classes
{
    public class OptimizerInitClass
    {
        private const double _actionCountRequired = 0.1;
        private Dictionary<OperationType, double> _minTrainingProfitRequired = new() {
            { OperationType.Buy, 0 },
            { OperationType.Sell, 0 }
        };

        private readonly double _commission;
        private readonly int _roundPoint;


        private readonly DataModel[] _sourceData;
        private readonly int _testSize;
        private readonly int _validationSize;
        private readonly IntRangeStruct _rsiRange;
        private readonly Dictionary<OperationType, DoubleRangeStruct> _stopLossRange = new();
        private readonly Dictionary<OperationType, DoubleRangeStruct> _takeProfitRange = new();
        private readonly IntRangeStruct _lastRsiSequence;
        private readonly int _horizon;
        private readonly string _instrument;

        private readonly Thread _saveThread;
        private readonly ConcurrentQueue<SavedModel> _parametersQueue = new();

        private List<SavedModel> _parametersSaved = new();
        
        private int _modelsLeft;

        private readonly DoubleRangeStruct _rsiBuyLimits;
        private readonly DoubleRangeStruct _rsiSellLimits;

        private readonly string _modelFileName;


        private bool _completionToken = false;

        private Dictionary<int, DataModel[]> RsiEnrichedCollection { get; set; } = new();
        private Dictionary<int, ReadOnlyMemory<SequenceClass>> TrainSet { get; set; } = new();
        private Dictionary<int, ReadOnlyMemory<SequenceClass>> TestSet { get; set; } = new();
        private Dictionary<int, ReadOnlyMemory<SequenceClass>> ValidationSet { get; set; } = new();
        private Dictionary<int, SequenceClass> LastPrice { get; set; } = new();

        public OptimizerInitClass(int testSize, int validationSize, IntRangeStruct rsiRange, DoubleRangeStruct rsiBuyLimits, DoubleRangeStruct rsiSellLimits, DoubleRangeStruct stopLossRange, DoubleRangeStruct takeProfitRange, DataModel[] data, IntRangeStruct lastRsiSequence, int horizon, DateTime restrictByDate, double commission, string instrument, int roundPoint) {
            _rsiBuyLimits = rsiBuyLimits;
            _rsiSellLimits = rsiSellLimits;
            _instrument = instrument;
            _commission = commission;
            _roundPoint = roundPoint;
            _horizon = horizon;
            _lastRsiSequence = lastRsiSequence;
            _testSize = testSize;
            _validationSize = validationSize;
            _rsiRange = rsiRange;
            _sourceData = data;

            Dictionary<OperationType, List<double>> changes = new() {
                { OperationType.Buy, new() },
                { OperationType.Sell, new() }
            };

            for (int i = 1; i < _sourceData.Length; i++) {
                double change = (_sourceData[i].Data[(int)DataColumn.Close] - _sourceData[i - 1].Data[(int)DataColumn.Close]) / _sourceData[i - 1].Data[(int)DataColumn.Close];
                if (change > 0)
                    changes[OperationType.Buy].Add(change);
                else if (change < 0)
                    changes[OperationType.Sell].Add(Math.Abs(change));
            }

            _stopLossRange = new() {
                { OperationType.Buy, new(min: Measures.Quantile(changes[OperationType.Buy].ToArray(), stopLossRange.Min), max: Measures.Quantile(changes[OperationType.Buy].ToArray(), stopLossRange.Max)) },
                { OperationType.Sell, new(min: Measures.Quantile(changes[OperationType.Sell].ToArray(), stopLossRange.Min), max: Measures.Quantile(changes[OperationType.Sell].ToArray(), stopLossRange.Max)) }
            };

            _takeProfitRange = new() {
                { OperationType.Buy, new(min: Measures.Quantile(changes[OperationType.Buy].ToArray(), takeProfitRange.Min), max: Measures.Quantile(changes[OperationType.Buy].ToArray(), takeProfitRange.Max) * (1 + _horizon * 0.1)) },
                { OperationType.Sell, new(min: Measures.Quantile(changes[OperationType.Sell].ToArray(), takeProfitRange.Min), max: Measures.Quantile(changes[OperationType.Sell].ToArray(), takeProfitRange.Max) * (1 + _horizon * 0.1)) }
            };
               
            InitializeRsi(restrictByDate);
            CreateSequences();

            _modelFileName = $"{_instrument}.{_horizon}.trained";

            _saveThread = new Thread(new ThreadStart(SaveThread));
        }

        private void CreateSequences() {
            int validationEndIndex = RsiEnrichedCollection[_rsiRange.Min].Length - _testSize - _validationSize;
            int testEndIndex = RsiEnrichedCollection[_rsiRange.Min].Length - _testSize;

            foreach (var data in RsiEnrichedCollection) {
                List<SequenceClass> trainSequences = new();
                List<SequenceClass> testSequences = new();
                List<SequenceClass> validationSequence = new();

                for (int i = _lastRsiSequence.Max; i < validationEndIndex; i++)
                    trainSequences.Add(new(before: data.Value[(i - _lastRsiSequence.Max)..i], after: data.Value[i..(i + _horizon)]));

                for (int i = validationEndIndex; i < testEndIndex; i++)
                    validationSequence.Add(new(before: data.Value[(i - _lastRsiSequence.Max)..i], after: data.Value[i..(i + _horizon)]));

                for (int i = testEndIndex; i < data.Value.Length - _horizon + 1; i++)
                    testSequences.Add(new(before: data.Value[(i - _lastRsiSequence.Max)..i], after: data.Value[i..(i + _horizon)]));

                LastPrice.Add(data.Key, new(before: data.Value[^_lastRsiSequence.Max..], after: null));

                TrainSet.Add(data.Key, trainSequences.ToArray());
                TestSet.Add(data.Key, testSequences.ToArray());
                ValidationSet.Add(data.Key, validationSequence.ToArray());
            }
        }

        private void DisplayResults() {
            Console.WriteLine("Optimization done");

            List<SavedModel> saved = LoadSavedModels();
            saved.ForEach(row => row.Parameters.ToOptimizableArray());

            List<PredictionModel> predictions = new();

            saved.GroupBy(m => m.Parameters.Operation).ToList().ForEach(operation => {
                Console.WriteLine(operation.Key);

                IEnumerable<SavedModel> top = operation.Where(m => m.TestedPerformance.Score > 0);

                StrategyClass strategy = new(models: top, operation: operation.Key, roundPoint: _roundPoint, commission: _commission, distanceBetweenOrders: _stopLossRange[operation.Key].Min);
                strategy.LoadSequences(TestSet);
                predictions.AddRange(strategy.Test());

                Console.WriteLine($"Total profit of the strategy: {(strategy.Profit / _commission):N3}");

            });

            try {
                SqliteExportClass exporter = new($"{_modelFileName}");
                exporter.PushPredictions(predictions);
                exporter.PushInstrumentData(_sourceData.Where(m => m.Id >= predictions.Min(m => m.Id)).ToArray());
            }
            catch { }
        }

        private List<ParametersModel> GenerateInitPoints(int count) {
            List<ParametersModel> returnVar = new();

            for (int i = 0; i < count; i++) {
                PointStruct buyConstant = new(range: _rsiBuyLimits, val: _rsiBuyLimits.Min + (_rsiBuyLimits.Max - _rsiBuyLimits.Min).GetRandomDouble());
                PointStruct sellConstant = new(range: _rsiSellLimits, val: _rsiSellLimits.Min + (_rsiSellLimits.Max - _rsiSellLimits.Min).GetRandomDouble());

                int lastPointSequence = (_lastRsiSequence.Max - _lastRsiSequence.Min).GetRandomInt() + _lastRsiSequence.Min;

                double buySlope = lastPointSequence == 0 ? 0 : ((buyConstant.Value - _rsiBuyLimits.Min) / lastPointSequence).GetRandomDouble();
                double sellSlope = lastPointSequence == 0 ? 0 : ((_rsiSellLimits.Max - sellConstant.Value) / lastPointSequence).GetRandomDouble();

                returnVar.Add(new(
                    rsiPeriod: (_rsiRange.Max - _rsiRange.Min).GetRandomInt() + _rsiRange.Min,
                    rsiLimits: buyConstant,
                    stopLoss: new(_stopLossRange[OperationType.Buy], (_stopLossRange[OperationType.Buy].Max - _stopLossRange[OperationType.Buy].Min).GetRandomDouble() + _stopLossRange[OperationType.Buy].Min),
                    takeProfit: new(_takeProfitRange[OperationType.Buy], (_takeProfitRange[OperationType.Buy].Max - _takeProfitRange[OperationType.Buy].Min).GetRandomDouble() + _takeProfitRange[OperationType.Buy].Min),
                    weights: new double[] { buyConstant.Value, buySlope },
                    indicatorLastPointSequence: lastPointSequence,
                    offset: new double[] { 0.10d.GetRandomDouble(), 0.001d.GetRandomDouble() - 0.0005, 0, 0 },
                    operation: OperationType.Buy,
                    rsquaredCutOff: new(range: new(min: 0.25, max: 0.95), 0.25 + 0.7d.GetRandomDouble()),
                    standardDeviationCorrection: 0,
                    rsiSlopeFitCorrection: 0));

                returnVar.Add(new(
                    rsiPeriod: (_rsiRange.Max - _rsiRange.Min).GetRandomInt() + _rsiRange.Min,
                    rsiLimits: sellConstant,
                    stopLoss: new(_stopLossRange[OperationType.Sell], (_stopLossRange[OperationType.Sell].Max - _stopLossRange[OperationType.Sell].Min).GetRandomDouble() + _stopLossRange[OperationType.Sell].Min),
                    takeProfit: new(_takeProfitRange[OperationType.Sell], (_takeProfitRange[OperationType.Sell].Max - _takeProfitRange[OperationType.Sell].Min).GetRandomDouble() + _takeProfitRange[OperationType.Sell].Min),
                    weights: new double[] { sellConstant.Value, sellSlope },
                    indicatorLastPointSequence: lastPointSequence,
                    offset: new double[] { 0.10d.GetRandomDouble(), 0.001d.GetRandomDouble() - 0.0005, 0, 0 },
                    operation: OperationType.Sell,
                    rsquaredCutOff: new(range: new(min: 0.25, max: 0.95), 0.25 + 0.7d.GetRandomDouble()),
                    standardDeviationCorrection: 0,
                    rsiSlopeFitCorrection: 0));
            }

            _modelsLeft = returnVar.Count;
            Console.WriteLine($"Models created for evaluation: {_modelsLeft}");

            return returnVar;
        }

        private void InitializeRsi(DateTime restrictByDate) {
            for (int i = _rsiRange.Min; i < _rsiRange.Max; i++) {
                RsiClass rsi = new(period: i);
                DataModel[] data = _sourceData.Copy();
                
                data = rsi.GetRSiOriginal(data, (int)DataColumn.Close, (int)DataColumn.Rsi);
                data = data
                    .Where(m => m.Date >= restrictByDate)
                    .OrderBy(m => m.Date)
                    .ToArray();

                RsiEnrichedCollection.Add(i, data);
            }
        }

        private List<SavedModel> LoadSavedModels() {

            if (!File.Exists(_modelFileName))
                return new();

            string content = File.ReadAllText(_modelFileName);

            if (content == string.Empty)
                return new();

            List<SavedModel> returnVar = new();
            try {
                returnVar = JsonConvert.DeserializeObject<List<SavedModel>>(content);
            }
            catch (Exception exception) {
                Console.WriteLine($"{exception.Message}");
            }
            return returnVar;
        }

        private void OptimizationRunner(List<ParametersModel> parameters, int degOfParal) => parameters.AsParallel().WithDegreeOfParallelism(degOfParal).ForAll(parameter => {
            if (_completionToken)
                return;

            OptimizerClass optimizer = new(sequences: TrainSet[parameter.RsiPeriod], parameter: parameter, commission: _commission, roundPoint: _roundPoint);
            var scoreChange = optimizer.Optimize(_minTrainingProfitRequired[parameter.Operation]);

            if (optimizer.IsSuccess) {
                optimizer.LoadSequence(ValidationSet[parameter.RsiPeriod], false);
                optimizer.Validate();

                if (optimizer.IsSuccess && ((double)optimizer.Performance[ExecutionType.Test].ActionCount / _testSize) >= _actionCountRequired) {
                    double improvement = scoreChange.postTrainScore / scoreChange.preTrainScore - 1;

                    Console.WriteLine($"Before: {scoreChange.preTrainScore:N3}\tAfter: {scoreChange.postTrainScore:N3}\t{improvement:N2}\t{parameter.Operation}");

                    _parametersQueue.Enqueue(new(parameters: parameter.Copy(), tested: optimizer.Performance[ExecutionType.Test], trained: optimizer.Performance[ExecutionType.Train]));
                }
            }

            _modelsLeft--;
        });

        public void Predict() {
            List<SavedModel> savedModels = LoadSavedModels();
            Console.WriteLine($"{_instrument}");
            Console.WriteLine($"Date: {LastPrice.First().Value.Date}");
            Console.WriteLine($"Current close: {LastPrice.First().Value.CurrentClosePrice}");

            savedModels.GroupBy(m => m.Parameters.Operation).ToList().ForEach(opGrouped => {
                Console.WriteLine($"Operation:\t{opGrouped.Key}");
                IEnumerable<SavedModel> models = opGrouped.Where(m => m.TestedPerformance.Score > 0);
                StrategyClass strategy = new(models: models, operation: opGrouped.Key, roundPoint: _roundPoint, commission: _commission, distanceBetweenOrders: _stopLossRange[opGrouped.Key].Min);
                strategy.LoadSequence(LastPrice);
                strategy.PredictLast();
            });
        }

        private void SaveThread() {
            while (!_completionToken || _parametersQueue.Count != 0) {

                List<SavedModel> newModels = new();

                while (_parametersQueue.TryDequeue(out SavedModel newModel))
                    newModels.Add(newModel);

                if (newModels.Count != 0) {
                    newModels.ForEach(newModel => {
                        List<SavedModel> existingModels = _parametersSaved.Where(m => m.Parameters.Operation == newModel.Parameters.Operation).ToList();

                        StrategyClass existingStrategy = new(existingModels, newModel.Parameters.Operation, _roundPoint, _commission, distanceBetweenOrders: _stopLossRange[newModel.Parameters.Operation].Min);
                        existingStrategy.LoadSequences(ValidationSet);
                        existingStrategy.Test();

                        existingModels.Add(newModel);

                        StrategyClass newStrategy = new(existingModels, newModel.Parameters.Operation, _roundPoint, _commission, distanceBetweenOrders: _stopLossRange[newModel.Parameters.Operation].Min);
                        newStrategy.LoadSequences(ValidationSet);
                        newStrategy.Test();

                        if (newStrategy.Profit > existingStrategy.Profit)
                            _parametersSaved.Add(newModel);
                    });

                    List<SavedModel> reducedModels = new();

                    var groupedByOperation = _parametersSaved.GroupBy(m => m.Parameters.Operation);
                    foreach (var group in groupedByOperation) {

                        var randomizedModels = group.OrderBy(m => 1d.GetRandomDouble()).ToList();
                        int tryCount = (int)Math.Floor(randomizedModels.Count * 0.1);
                        var optimizedModels = randomizedModels;

                        if (tryCount > 0) {
                            StrategyClass fullStrategy = new(randomizedModels, group.Key, _roundPoint, _commission, distanceBetweenOrders: _stopLossRange[group.Key].Min);
                            fullStrategy.LoadSequences(ValidationSet);
                            fullStrategy.Test();

                            double profit = fullStrategy.Profit;

                            for (int i = 0; i < tryCount; i++) {

                                List<SavedModel> reducedList = randomizedModels.Copy();
                                reducedList.RemoveAt(i);

                                StrategyClass reducedStrategy = new(reducedList, group.Key, _roundPoint, _commission, distanceBetweenOrders: _stopLossRange[group.Key].Min);
                                reducedStrategy.LoadSequences(ValidationSet);
                                reducedStrategy.Test();

                                if (reducedStrategy.Profit > profit) {
                                    optimizedModels = reducedList;
                                    profit = reducedStrategy.Profit;
                                }
                            }
                        }
                        reducedModels.AddRange(optimizedModels);

                    }

                    _parametersSaved = reducedModels;

                    SaveModels(_parametersSaved);
                }

                if (!_completionToken)
                    Thread.Sleep(10000);
                else
                    break;
            }

            List<SavedModel> finalSavedFiltered = new();

            _parametersSaved.GroupBy(m => m.Parameters.Operation).ToList().ForEach(operationGroup => finalSavedFiltered.AddRange(operationGroup.SelectDistinct()));

            SaveModels(finalSavedFiltered);

        }

        public void StartOptimization(int randomInitCount, int degreeOfParallelism = -1) {
            int degOfParal = degreeOfParallelism == -1 ? Environment.ProcessorCount : degreeOfParallelism;
            List<ParametersModel> initParameters = GenerateInitPoints(randomInitCount);

            List<ParametersModel> savedModels = LoadSavedModels().Select(m => m.Parameters).ToList();

            _saveThread.Start();

            if (savedModels.Count != 0) {
                _modelsLeft += savedModels.Count;
                OptimizationRunner(savedModels, degOfParal);
            }

            OptimizationRunner(initParameters, degOfParal);

            _completionToken = true;
            _saveThread.Join();

            DisplayResults();
        }

        private void SaveModels(List<SavedModel> models) {
            Console.WriteLine($"{DateTime.Now} Models left: {_modelsLeft}");

            try {
                File.WriteAllText(_modelFileName, JsonConvert.SerializeObject(models));
            }
            catch (Exception exception) {
                Console.WriteLine(exception.Message);
            }
        }

    }
}