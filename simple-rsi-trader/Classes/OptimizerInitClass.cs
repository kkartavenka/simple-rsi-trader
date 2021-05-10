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
            { OperationType.Buy, 0},
            { OperationType.Sell, 0}
        };

        private readonly DoubleRangeStruct _scoreScalingTo = new(min: 0.5, max: 0.75);

        private readonly int _saveTop;

        private readonly double _commission;
        private readonly int _roundPoint;

        private const double _defaultInterceptOffset = 0.5;

        private readonly DataModel[] _sourceData;
        private readonly int _testSize;
        private readonly int _validationSize;
        private readonly IntRangeStruct _rsiRange;
        private readonly DoubleRangeStruct _stopLossRange;
        private readonly DoubleRangeStruct _takeProfitRange;
        private readonly IntRangeStruct _lastRsiSequence;
        private readonly int _horizon;
        private readonly string _instrument;

        private readonly Thread _saveThread;
        private readonly ConcurrentQueue<SavedModel> _parametersQueue = new();
        private List<SavedModel> _parametersSaved = new();
        private ConcurrentQueue<(OperationType, double)> _optimizationImprovementQueue = new();
        private Dictionary<OperationType, List<double>> _optimizationImprovement = new();

        private int _modelsLeft;

        private readonly DoubleRangeStruct _rsiBuyLimits;
        private readonly DoubleRangeStruct _rsiSellLimits;


        private readonly string _modelFileName;


        private bool _completionToken = false;

        private Dictionary<int, DataModel[]> RsiEnrichedCollection { get; set; } = new();
        private Dictionary<int, SequenceClass[]> TrainSet { get; set; } = new();
        private Dictionary<int, SequenceClass[]> TestSet { get; set; } = new();
        private Dictionary<int, SequenceClass[]> ValidationSet { get; set; } = new();
        private Dictionary<int, SequenceClass> LastPrice { get; set; } = new();

        public OptimizerInitClass(int testSize, int validationSize, IntRangeStruct rsiRange, DoubleRangeStruct rsiBuyLimits, DoubleRangeStruct rsiSellLimits, DoubleRangeStruct stopLossRange, DoubleRangeStruct takeProfitRange, DataModel[] data, IntRangeStruct lastRsiSequence, int horizon, DateTime restrictByDate, double commission, string instrument, int saveTop, int roundPoint) {
            _rsiBuyLimits = rsiBuyLimits;
            _rsiSellLimits = rsiSellLimits;
            _saveTop = saveTop;
            _instrument = instrument;
            _commission = commission;
            _roundPoint = roundPoint;
            _horizon = horizon;
            _lastRsiSequence = lastRsiSequence;
            _testSize = testSize;
            _validationSize = validationSize;
            _rsiRange = rsiRange;
            _stopLossRange = new(min: stopLossRange.Min * _commission, max: stopLossRange.Max * _commission);
            _takeProfitRange = new(min: takeProfitRange.Min * _commission, max: takeProfitRange.Max * _commission);
            _sourceData = data;

            InitializeRsi(restrictByDate);
            CreateSequences();

            _modelFileName = $"{_instrument}.{_horizon}.trained";

            _saveThread = new Thread(new ThreadStart(SaveThread));
            _saveThread.Start();
        }

        private void CreateSequences() {
            int validationEndIndex = RsiEnrichedCollection[_rsiRange.Min].Length - _testSize - _validationSize;
            int testEndIndex = RsiEnrichedCollection[_rsiRange.Min].Length - _testSize;

            foreach (KeyValuePair<int, DataModel[]> data in RsiEnrichedCollection) {
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

                StrategyClass strategy = new(models: top, operation: operation.Key, roundPoint: _roundPoint, commission: _commission);
                strategy.LoadSequences(TestSet);
                predictions.AddRange(strategy.Test());

                Console.WriteLine($"Total profit of the strategy: {(strategy.Profit / _commission):N3}");

            });

            SqliteExportClass exporter = new($"{_modelFileName}");
            exporter.PushPredictions(predictions);
            exporter.PushInstrumentData(_sourceData.Where(m => m.Id >= predictions.Min(m => m.Id)).ToArray());
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
                    stopLoss: new(_stopLossRange, (_stopLossRange.Max - _stopLossRange.Min).GetRandomDouble() + _stopLossRange.Min),
                    takeProfit: new(_takeProfitRange, (_takeProfitRange.Max - _takeProfitRange.Min).GetRandomDouble() + _takeProfitRange.Min),
                    weights: new double[] { buyConstant.Value, buySlope },
                    indicatorLastPointSequence: lastPointSequence,
                    offset: new double[] { 0.15d.GetRandomDouble(), 0.001d.GetRandomDouble(), 0.01d.GetRandomDouble() },
                    operation: OperationType.Buy));

                returnVar.Add(new(
                    rsiPeriod: (_rsiRange.Max - _rsiRange.Min).GetRandomInt() + _rsiRange.Min,
                    rsiLimits: sellConstant,
                    stopLoss: new(_stopLossRange, (_stopLossRange.Max - _stopLossRange.Min).GetRandomDouble() + _stopLossRange.Min),
                    takeProfit: new(_takeProfitRange, (_takeProfitRange.Max - _takeProfitRange.Min).GetRandomDouble() + _takeProfitRange.Min),
                    weights: new double[] { sellConstant.Value, sellSlope },
                    indicatorLastPointSequence: lastPointSequence,
                    offset: new double[] { 0.15d.GetRandomDouble(), 0.001d.GetRandomDouble(), 0.01d.GetRandomDouble() },
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
            OptimizerClass optimizer = new(sequences: TrainSet[parameter.RsiPeriod], parameter: parameter, commission: _commission, roundPoint: _roundPoint);
            var scoreChange = optimizer.Optimize(_minTrainingProfitRequired[parameter.Operation]);

            if (optimizer.IsSuccess) {
                optimizer.LoadSequence(ValidationSet[parameter.RsiPeriod], false);
                optimizer.Validate();

                if (optimizer.IsSuccess && ((double)optimizer.Performance[ExecutionType.Test].ActionCount / _testSize) >= _actionCountRequired) {
                    double improvement = scoreChange.postTrainScore / scoreChange.preTrainScore - 1;

                    Console.WriteLine($"Before: {scoreChange.preTrainScore:N3}\tAfter: {scoreChange.postTrainScore:N3}\t{improvement:N2}\t{parameter.Operation}");
                    
                    if (improvement > 0)
                        _optimizationImprovementQueue.Enqueue((parameter.Operation, improvement));

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
                StrategyClass strategy = new(models: models, operation: opGrouped.Key, roundPoint: _roundPoint, commission: _commission);
                strategy.LoadSequence(LastPrice);
                strategy.PredictLast();
            });
        }

        private void SaveThread() {
            while (!_completionToken || _parametersQueue.Count != 0) {
                while (_parametersQueue.TryDequeue(out SavedModel newModel))
                    _parametersSaved.Add(newModel);

                List<SavedModel> savedFiltered = new();
                List<SavedModel> intermediateFiltered = new();

                while(_optimizationImprovementQueue.TryDequeue(out var optimization)) {
                    if (_optimizationImprovement.ContainsKey(optimization.Item1))
                        _optimizationImprovement[optimization.Item1].Add(optimization.Item2);
                    else
                        _optimizationImprovement.Add(optimization.Item1, new List<double>() { optimization.Item2 });
                }

                _parametersSaved.GroupBy(m => m.Parameters.Operation)
                    .ToList().ForEach(operationGroup => {

                        double minProfit = 0;

                        List<SavedModel> operationGroupList = operationGroup.Distinct(new SavedModelComparerClass()).ToList();
                        

                        if (operationGroupList.Count > _saveTop) {

                            List<double[]> selectedData = operationGroupList.Select(m => new double[] { m.TrainedPerformance.Profit, m.TrainedPerformance.LossRate + _defaultInterceptOffset, m.TestedPerformance.LossRate + _defaultInterceptOffset, m.TestedPerformance.Profit, m.TrainedPerformance.WinRate, m.TestedPerformance.WinRate }).ToList();

                            List<string> outputLines = new List<string>();
                            selectedData.ForEach(row => outputLines.Add(string.Join(',', row)));

                            File.WriteAllLines($"{_modelFileName}.csv", outputLines);

                            OrdinaryLeastSquares ols = new OrdinaryLeastSquares() { UseIntercept = false };
                            SimpleLinearRegression reg1 = ols.Learn(selectedData.Select(m => m[0]).ToArray(), selectedData.Select(m => m[1]).ToArray());
                            SimpleLinearRegression reg2 = ols.Learn(selectedData.Select(m => m[0]).ToArray(), selectedData.Select(m => m[2]).ToArray());
                            SimpleLinearRegression reg3 = ols.Learn(selectedData.Select(m => m[3]).ToArray(), selectedData.Select(m => m[2]).ToArray());

                            operationGroupList.ForEach(row => {
                                double regL1 = reg1.Transform(row.TrainedPerformance.Profit) - row.TrainedPerformance.LossRate - _defaultInterceptOffset;
                                double regL2 = reg1.Transform(row.TrainedPerformance.Profit) - row.TestedPerformance.LossRate - _defaultInterceptOffset;
                                double regL3 = reg1.Transform(row.TestedPerformance.Profit) - row.TestedPerformance.LossRate - _defaultInterceptOffset;

                                if (regL1 > 0 && regL2 > 0 && regL3 > 0)
                                    row.TestedPerformance.Score = regL1 + regL2 + regL3;
                                else
                                    row.TestedPerformance.Score = 0;

                            });

                            IEnumerable<SavedModel> selected = operationGroupList.OrderByDescending(m => m.TestedPerformance.Score).ThenByDescending(m => m.TrainedPerformance.Profit).Take(_saveTop * 3);
                            minProfit = selected.Min(m => m.TrainedPerformance.Profit);

                            intermediateFiltered.AddRange(selected);
                            savedFiltered.AddRange(selected.Take(_saveTop).ToList());
                        }
                        else {
                            intermediateFiltered.AddRange(operationGroupList);
                            savedFiltered.AddRange(operationGroupList);
                        }

                        _minTrainingProfitRequired[operationGroup.Key] = minProfit * 0.05;
                    });

                if (intermediateFiltered.Count != 0) {
                    _parametersSaved = intermediateFiltered;
                    SaveModels(savedFiltered);
                }

                if (!_completionToken)
                    Thread.Sleep(10000);
                else
                    break;
            }
        }

        public void StartOptimization(int randomInitCount, int degreeOfParallelism = -1) {

            int degOfParal = degreeOfParallelism == -1 ? Environment.ProcessorCount : degreeOfParallelism;
            List<ParametersModel> initParameters = GenerateInitPoints(randomInitCount);

            List<ParametersModel> savedModels = LoadSavedModels().Select(m => m.Parameters).ToList();

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