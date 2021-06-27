using Accord.Math.Distances;
using Accord.Math.Optimization;
using Accord.Statistics;
using Accord.Statistics.Models.Regression.Linear;
using CommonLib.Classes;
using CommonLib.Classes.DeepCopy;
using CommonLib.Enums;
using CommonLib.Extensions;
using CommonLib.Indicators;
using CommonLib.Models;
using CommonLib.Models.Export;
using CommonLib.Models.Range;
using Newtonsoft.Json;
using simple_trader.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using static CommonLib.Models.DataModel;
using static simple_trader.Classes.OptimizerClass;

namespace simple_trader.Classes
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
        private readonly IntRangeStruct _sequenceRange;
        private readonly Dictionary<OperationType, DoubleRangeStruct> _stopLossRange = new();
        private readonly Dictionary<OperationType, DoubleRangeStruct> _takeProfitRange = new();
        private readonly int _horizon;
        private readonly string _instrument;

        private readonly Thread _saveThread;
        private readonly ConcurrentQueue<SavedModel> _parametersQueue = new();

        private List<SavedModel> _parametersSaved = new();
        
        private int _modelsLeft;

        private readonly string _modelFileName;


        private bool _completionToken = false;

        private Dictionary<int, DataModel[]> SlopeEnrichedCollection { get; set; } = new();
        private Dictionary<int, DoubleRangeStruct> SlopeLimits { get; set; } = new();
        private Dictionary<int, ReadOnlyMemory<SequenceClass>> TrainSet { get; set; } = new();
        private Dictionary<int, ReadOnlyMemory<SequenceClass>> TestSet { get; set; } = new();
        private Dictionary<int, ReadOnlyMemory<SequenceClass>> ValidationSet { get; set; } = new();
        private Dictionary<int, SequenceClass> LastPrice { get; set; } = new();

        public OptimizerInitClass(int testSize, int validationSize, IntRangeStruct sequenceRange, DoubleRangeStruct stopLossRange, DoubleRangeStruct takeProfitRange, DataModel[] data, int horizon, DateTime restrictByDate, double commission, string instrument, int roundPoint) {
            _instrument = instrument;
            _commission = commission;
            _roundPoint = roundPoint;
            _horizon = horizon;
            _testSize = testSize;
            _validationSize = validationSize;
            _sequenceRange = sequenceRange;
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
                { OperationType.Buy, new(min: Measures.Quantile(changes[OperationType.Buy].ToArray(), takeProfitRange.Min), max: Measures.Quantile(changes[OperationType.Buy].ToArray(), takeProfitRange.Max) * (1 + _horizon * 0.05)) },
                { OperationType.Sell, new(min: Measures.Quantile(changes[OperationType.Sell].ToArray(), takeProfitRange.Min), max: Measures.Quantile(changes[OperationType.Sell].ToArray(), takeProfitRange.Max) * (1 + _horizon * 0.05)) }
            };

            RsiClass rsi = new(period: 10);
            _sourceData = rsi.GetRSiOriginal(_sourceData, (int)DataColumn.Close, (int)DataColumn.Rsi);

            MfiClass mfi = new(period: 10);
            _sourceData = mfi.GetMfi(model: _sourceData, columnSignalIndex: (int)DataColumn.TypicalPrice, columnVolumeIndex: (int)DataColumn.Volume, columnDestinationIndex: (int)DataColumn.Mfi);

            InitializeSequence(restrictByDate);
            CreateSequences();

            _modelFileName = $"{_instrument}.{_horizon}.trained";

            _saveThread = new Thread(new ThreadStart(SaveThread));
        }

        private void CreateSequences() {
            int validationEndIndex = SlopeEnrichedCollection[_sequenceRange.Min].Length - _testSize - _validationSize;
            int testEndIndex = SlopeEnrichedCollection[_sequenceRange.Min].Length - _testSize;

            foreach (var data in SlopeEnrichedCollection) {
                List<SequenceClass> trainSequences = new();
                List<SequenceClass> testSequences = new();
                List<SequenceClass> validationSequence = new();

                for (int i = _sequenceRange.Max; i < validationEndIndex; i++)
                    trainSequences.Add(new(before: data.Value[(i - _sequenceRange.Max)..i], after: data.Value[i..(i + _horizon)], priceSequenceLimit: data.Key));

                for (int i = validationEndIndex; i < testEndIndex; i++)
                    validationSequence.Add(new(before: data.Value[(i - _sequenceRange.Max)..i], after: data.Value[i..(i + _horizon)], priceSequenceLimit: data.Key));

                for (int i = testEndIndex; i < data.Value.Length - _horizon + 1; i++)
                    testSequences.Add(new(before: data.Value[(i - _sequenceRange.Max)..i], after: data.Value[i..(i + _horizon)], priceSequenceLimit: data.Key));

                LastPrice.Add(data.Key, new(before: data.Value[^_sequenceRange.Max..], after: null, priceSequenceLimit: data.Key));

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
                IEnumerable<SavedModel> top = operation.Where(m => m.TestedPerformance.Score > 0);

                StrategyClass strategy = new(models: top, operation: operation.Key, roundPoint: _roundPoint, commission: _commission, distanceBetweenOrders: _stopLossRange[operation.Key].Min);
                strategy.LoadSequences(TestSet);
                predictions.AddRange(strategy.Test());

                Console.WriteLine($"{operation.Key}\tProfit: {(strategy.Profit / _commission):N3}\tScore {strategy.Score / _commission:N3}\tWin {strategy.WinCount}\tLoss {strategy.LossCount}");

            });

            try {
                SqliteExportClass exporter = new($"{_modelFileName}");
                exporter.PushPredictions(predictions);
                exporter.PushInstrumentData(_sourceData.Where(m => m.Id >= predictions.Min(m => m.Id - 7)).ToArray());
            }
            catch { }
        }

        private List<ParametersModel> GenerateInitPoints(int count) {
            List<ParametersModel> returnVar = new();

            for (int i = 0; i < count; i++) {
                int sequenceLength = (_sequenceRange.Max - _sequenceRange.Min).GetRandomInt() + _sequenceRange.Min;

                returnVar.Add(new(
                    sequenceLength: sequenceLength,
                    slopeLimits: new(range: new(min: SlopeLimits[sequenceLength].Min, max: 0), val: SlopeLimits[sequenceLength].Min.GetRandomDouble()),
                    slopeLimitsRSquared: new(new(0.2, 1), 0.2 + 0.8d.GetRandomDouble()),
                    stopLoss: new(_stopLossRange[OperationType.Buy], (_stopLossRange[OperationType.Buy].Max - _stopLossRange[OperationType.Buy].Min).GetRandomDouble() + _stopLossRange[OperationType.Buy].Min),
                    takeProfit: new(_takeProfitRange[OperationType.Buy], (_takeProfitRange[OperationType.Buy].Max - _takeProfitRange[OperationType.Buy].Min).GetRandomDouble() + _takeProfitRange[OperationType.Buy].Min),
                    offset: new double[] { 0.1d.GetRandomDouble(), 0.001d.GetRandomDouble() - 0.0005, 0},
                    operation: OperationType.Buy,
                    mfi: new double[] { 0.001d.GetRandomDouble() - 0.0005, 0 },
                    standardDeviationCorrection: 0,
                    rsi: new double[] { 0.001d.GetRandomDouble() - 0.0005, 0 },
                    slopeRSquaredFitCorrection: 0));

                returnVar.Add(new(
                    sequenceLength: sequenceLength,
                    slopeLimits: new(range: new(min: 0, max: SlopeLimits[sequenceLength].Max), val: SlopeLimits[sequenceLength].Max.GetRandomDouble()),
                    slopeLimitsRSquared: new(new(0.2, 1), 0.2 + 0.8d.GetRandomDouble()),
                    stopLoss: new(_stopLossRange[OperationType.Sell], (_stopLossRange[OperationType.Sell].Max - _stopLossRange[OperationType.Sell].Min).GetRandomDouble() + _stopLossRange[OperationType.Sell].Min),
                    takeProfit: new(_takeProfitRange[OperationType.Sell], (_takeProfitRange[OperationType.Sell].Max - _takeProfitRange[OperationType.Sell].Min).GetRandomDouble() + _takeProfitRange[OperationType.Sell].Min),
                    offset: new double[] { 0.1d.GetRandomDouble(), 0.001d.GetRandomDouble() - 0.0005, 0},
                    operation: OperationType.Sell,
                    mfi: new double[] { 0.001d.GetRandomDouble() - 0.0005, 0 },
                    standardDeviationCorrection: 0,
                    rsi: new double[] { 0.001d.GetRandomDouble() - 0.0005, 0 },
                    slopeRSquaredFitCorrection: 0));
            }

            _modelsLeft = returnVar.Count;
            Console.WriteLine($"Models created for evaluation: {_modelsLeft}");

            return returnVar;
        }

        private void InitializeSequence(DateTime restrictByDate) {
            for (int i = _sequenceRange.Min; i < _sequenceRange.Max; i++) {
                DataModel[] data = _sourceData.Copy();

                double minValue = double.MaxValue;
                double maxValue = double.MinValue;

                double[] x = new double[i];
                for (int j = 0; j < x.Length; j++) x[j] = j;

                for (int j = i; j < data.Length; j++) {

                    double[] y = data[(j - i + 1)..(j + 1)].Select(m => m.Data[(int)DataColumn.Close]).ToArray();

                    OrdinaryLeastSquares ols = new();
                    SimpleLinearRegression slr = ols.Learn(x, y);
                    
                    data[j].Data[(int)DataColumn.ClosePriceSlope] = slr.Slope;

                    ReadOnlySpan<double> expectedY = new ReadOnlySpan<double>(slr.Transform(x));
                    data[j].Data[(int)DataColumn.ClosePriceSlopeRSquared] = expectedY.RSquared(y);

                    if (minValue > slr.Slope)
                        minValue = slr.Slope;

                    if (maxValue < slr.Slope)
                        maxValue = slr.Slope;
                }

                SlopeEnrichedCollection.Add(i, data);
                SlopeLimits.Add(i, new(min: minValue, max: maxValue));
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

            OptimizerClass optimizer = new(sequences: TrainSet[parameter.SequenceLength], parameter: parameter, commission: _commission, roundPoint: _roundPoint);
            var scoreChange = optimizer.Optimize(_minTrainingProfitRequired[parameter.Operation]);

            if (optimizer.IsSuccess) {
                optimizer.LoadSequence(ValidationSet[parameter.SequenceLength], false);
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


                    _parametersSaved = ReduceModels(0.1);

                    SaveModels(_parametersSaved);
                }

                if (!_completionToken)
                    Thread.Sleep(10000);
                else
                    break;
            }

            _parametersSaved = ReduceModels(1);
            SaveModels(_parametersSaved);
        }

        public List<SavedModel> ReduceModels(double tryRestrictor) {

            List<SavedModel> reducedModels = new();

            var groupedByOperation = _parametersSaved.GroupBy(m => m.Parameters.Operation);

            foreach (var group in groupedByOperation) {

                var randomizedModels = group.OrderBy(m => 1d.GetRandomDouble()).ToList();
                int tryCount = (int)Math.Floor(randomizedModels.Count * tryRestrictor);
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

            return reducedModels;
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