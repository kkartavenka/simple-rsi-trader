using CommonLib.Classes.DeepCopy;
using CommonLib.Extensions;
using CommonLib.Indicators;
using CommonLib.Models;
using CommonLib.Models.Range;
using simple_rsi_trader.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CommonLib.Models.DataModel;
using static simple_rsi_trader.Models.ParametersModel;

namespace simple_rsi_trader.Classes
{
    public class OptimizerInitClass
    {
        private const double _rsiConstantBuy = 50;
        private const double _rsiConstantSell = 50;
        private const double _rsiMin = 10;
        private const double _rsiMax = 90;


        private readonly DataModel[] _sourceData;
        private readonly int _testSize;
        private readonly int _validationSize;
        private readonly IntRangeStruct _rsiRange;
        private readonly DoubleRangeStruct _stopLossRange;
        private readonly DoubleRangeStruct _takeProfitRange;
        private readonly IntRangeStruct _lastRsiSequence;
        private readonly int _horizon;

        private Dictionary<int, DataModel[]> RsiEnrichedCollection { get; set; } = new Dictionary<int, DataModel[]>();

        private Dictionary<int, SequenceClass[]> TrainSet { get; set; } = new Dictionary<int, SequenceClass[]>();
        private Dictionary<int, SequenceClass[]> ValidationSet { get; set; } = new Dictionary<int, SequenceClass[]>();
        private Dictionary<int, SequenceClass[]> TestSet { get; set; } = new Dictionary<int, SequenceClass[]>();

        public OptimizerInitClass(int testSize, int validationSize, IntRangeStruct rsiRange, DoubleRangeStruct stopLossRange, DoubleRangeStruct takeProfitRange, DataModel[] data, IntRangeStruct lastRsiSequence, int horizon, DateTime restrictByDate)
        {
            _horizon = horizon;
            _lastRsiSequence = lastRsiSequence;
            _testSize = testSize;
            _validationSize = validationSize;
            _rsiRange = rsiRange;
            _stopLossRange = stopLossRange;
            _takeProfitRange = takeProfitRange;
            _sourceData = data;

            InitializeRsi(restrictByDate);
            CreateSequences();
        }

        private void CreateSequences()
        {
            int testTestEndIndex = RsiEnrichedCollection[_rsiRange.Min].Length - _testSize - _validationSize;
            int validationEndIndex = RsiEnrichedCollection[_rsiRange.Min].Length - _testSize;

            foreach (KeyValuePair<int, DataModel[]> data in RsiEnrichedCollection)
            {
                List<SequenceClass> trainSequences = new();
                List<SequenceClass> validationSequences = new();
                List<SequenceClass> testSequences = new();

                for (int i = _lastRsiSequence.Max; i < testTestEndIndex; i++)
                    trainSequences.Add(new(before: data.Value[(i - _lastRsiSequence.Max)..i], after: data.Value[i..(i + _horizon)]));

                for (int i = testTestEndIndex; i < validationEndIndex; i++)
                    validationSequences.Add(new(before: data.Value[(i - _lastRsiSequence.Max)..i], after: data.Value[i..(i + _horizon)]));

                for (int i = validationEndIndex; i < data.Value.Length - _horizon; i++)
                    testSequences.Add(new(before: data.Value[(i - _lastRsiSequence.Max)..i], after: data.Value[i..(i + _horizon)]));

                TrainSet.Add(data.Key, trainSequences.ToArray());
                ValidationSet.Add(data.Key, validationSequences.ToArray());
                TestSet.Add(data.Key, testSequences.ToArray());
            }
        }

        private List<ParametersModel> GenerateInitPoints(int count)
        {
            List<ParametersModel> returnVar = new();

            for (int i = 0; i < count; i++)
            {
                double buyConstant = _rsiConstantBuy.GetRandomDouble();
                double sellConstant = _rsiConstantSell + (_rsiMax - _rsiConstantSell).GetRandomDouble();

                int lastPointSequence = (_lastRsiSequence.Max - _lastRsiSequence.Min).GetRandomInt() + _lastRsiSequence.Min;

                double buySlope = ((buyConstant - _rsiMin) / lastPointSequence).GetRandomDouble();
                double sellSlope = ((_rsiMax - sellConstant) / lastPointSequence).GetRandomDouble();

                returnVar.Add(new(
                    rsiPeriod: (_rsiRange.Max - _rsiRange.Min).GetRandomInt() + _rsiRange.Min,
                    stopLoss: new(_stopLossRange, (_stopLossRange.Max - _stopLossRange.Min).GetRandomDouble() + _stopLossRange.Min),
                    takeProfit: new(_takeProfitRange, (_takeProfitRange.Max - _takeProfitRange.Min).GetRandomDouble() + _takeProfitRange.Min),
                    weights: new double[] { buyConstant, buySlope },
                    indicatorLastPointSequence: lastPointSequence,
                    operation: OperationType.Buy));

                returnVar.Add(new(
                    rsiPeriod: (_rsiRange.Max - _rsiRange.Min).GetRandomInt() + _rsiRange.Min,
                    stopLoss: new(_stopLossRange, (_stopLossRange.Max - _stopLossRange.Min).GetRandomDouble() + _stopLossRange.Min),
                    takeProfit: new(_takeProfitRange, (_takeProfitRange.Max - _takeProfitRange.Min).GetRandomDouble() + _takeProfitRange.Min),
                    weights: new double[] { sellConstant, sellSlope },
                    indicatorLastPointSequence: lastPointSequence,
                    operation: OperationType.Sell));
            }

            return returnVar;
        }

        private void InitializeRsi(DateTime restrictByDate)
        {
            for (int i = _rsiRange.Min; i < _rsiRange.Max; i++)
            {
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



        public void StartOptimization(int randomInitCount, int degreeOfParallelism = -1)
        {
            int degOfParal = degreeOfParallelism == -1 ? Environment.ProcessorCount : degreeOfParallelism;
            List<ParametersModel> initParameters = GenerateInitPoints(randomInitCount);

            initParameters.AsParallel().WithDegreeOfParallelism(degOfParal).ForAll(parameter =>
            {

            });
        }


    }
}
