using Accord.Statistics;
using CommonLib.Models;
using CommonLib.Models.Export;
using simple_rsi_trader.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static CommonLib.Enums.Enums;
using static simple_rsi_trader.Classes.OperationClass;

namespace simple_rsi_trader.Classes
{
    public class StrategyClass
    {
        private readonly double _orderDistance = 10;
        private readonly double _commission;
        private readonly List<SavedModel> _models;
        private readonly int _roundPoint;
        private readonly OperationType _operation;

        private Dictionary<int, SequenceClass[]> _testSet;
        private Dictionary<int, SequenceClass> _predictionSet;
        public StrategyClass(IEnumerable<SavedModel> models, OperationType operation, int roundPoint, double commission) {
            _commission = commission;
            _roundPoint = roundPoint;
            _operation = operation;
            _models = models.ToList();
            _models.ForEach(m => m.Parameters.ToOptimizableArray());

            _orderDistance = models.Select(m => m.Parameters.StopLoss.Value).ToArray().Mean() * 0.66;
        }

        private List<PredictionStruct> GetPredictions(Dictionary<int, SequenceClass> sequence) {
            List<PredictionStruct> preparedOrders = new();
            List<PredictionStruct> returnVar = new();

            _models.ForEach(m => {
                if (sequence[m.Parameters.RsiPeriod].CheckActivation(m.Parameters.OptimizableArray, m.Parameters))
                    preparedOrders.Add(sequence[m.Parameters.RsiPeriod].GetOrder(m.Parameters.OptimizableArray, m.Parameters, _roundPoint, m.TestedPerformance.Score));
            });

            if (preparedOrders.Count == 0)
                return returnVar;

            if (_operation == OperationType.Sell) {
                preparedOrders = preparedOrders.OrderBy(m => m.LimitOrder).ThenBy(m => m.StopLoss).ToList();
                double minLimitOrder = preparedOrders.First().LimitOrder;

                while (preparedOrders.Where(m => m.LimitOrder >= minLimitOrder).Count() > 0) {
                    List<PredictionStruct> similarOrders = preparedOrders.Where(m => m.LimitOrder >= minLimitOrder && m.LimitOrder < minLimitOrder + _orderDistance).ToList();
                    returnVar.Add(similarOrders.OrderByDescending(m => m.Score).First());

                    List<PredictionStruct> nextPoints = preparedOrders.Where(m => m.LimitOrder > similarOrders.Max(m => m.LimitOrder)).ToList();
                    minLimitOrder = nextPoints.Count != 0 ? nextPoints.First().LimitOrder : double.MaxValue;
                }
            }
            else if (_operation == OperationType.Buy) {
                preparedOrders = preparedOrders.OrderByDescending(m => m.LimitOrder).ThenByDescending(m => m.StopLoss).ToList();

                double maxLimitOrder = preparedOrders.First().LimitOrder;
                while (preparedOrders.Where(m=>m.LimitOrder <= maxLimitOrder).Count() > 0) {
                    List<PredictionStruct> similarOrders = preparedOrders.Where(m => m.LimitOrder <= maxLimitOrder && m.LimitOrder > maxLimitOrder - _orderDistance).ToList();
                    returnVar.Add(similarOrders.OrderByDescending(m => m.Score).First());

                    List<PredictionStruct> nextPoints = preparedOrders.Where(m => m.LimitOrder < similarOrders.Min(m => m.LimitOrder)).ToList();
                    maxLimitOrder = nextPoints.Count != 0 ? nextPoints.First().LimitOrder : double.MinValue;
                }
            }

            return returnVar;
        }

        public void LoadSequences(Dictionary<int, SequenceClass[]> testSet) => _testSet = testSet;

        public void LoadSequence(Dictionary<int, SequenceClass> predictionSet) => _predictionSet = predictionSet;

        public void PredictLast() {
            List<PredictionStruct> predictions = GetPredictions(_predictionSet);
            predictions.ForEach(row => Console.WriteLine($"Limit order: {row.LimitOrder}\tStop loss: {row.StopLoss}\tTakeProfit: {row.TakeProfit}"));
        }

        public List<PredictionModel> Test() {
            double profit = 0;
            int itemCount = _testSet.First().Value.Length;
            int actionCount = 0;
            List<int> rsiPeriod = _testSet.Keys.ToList();

            List<PredictionModel> export = new();

            for (int i = 0; i < itemCount; i++) {
                Dictionary<int, SequenceClass> preparedSequence = new();
                rsiPeriod.ForEach(rsiPeriod => preparedSequence.Add(rsiPeriod, _testSet[rsiPeriod][i]));

                List<PredictionStruct> predictions = GetPredictions(preparedSequence);

                SequenceClass sequence = preparedSequence.FirstOrDefault().Value;

                actionCount += predictions.Count;

                predictions.ForEach(prediction => {
                    OrderModel order = new(
                        endPeriodClosePrice: sequence.EndPeriodClosePrice,
                        order: prediction.LimitOrder,
                        firstLow: sequence.FirstPeriodLowPrice,
                        firstHigh: sequence.FirstPeriodHighPrice,
                        lowestPrice: sequence.LowestPrice,
                        highestPrice: sequence.HighestPrice,
                        nonFirstHighestPrice: sequence.NonFirstHighestPrice,
                        nonFirstLowestPrice: sequence.NonFirstLowestPrice);

                    if (i + 1 < _testSet[rsiPeriod[0]].Length)
                        export.Add(new(id: _testSet[rsiPeriod[0]][i + 1].Id, operation: _operation, prediction: prediction));

                    var result = order.AssessProfitFromOrder(operation: _operation, stopLoss: prediction.StopLossDistance, takeProfit: prediction.TakeProfitDistance, commission: _commission);
                    if (result.outcome != ActionOutcome.NoAction) {

                        profit += result.profit;
                    }
                });
            }

            IsSuccess = profit > 0;
            Profit = profit / actionCount;

            return export;
        }

        public bool IsSuccess { get; private set; }
        public double Profit { get; private set; }
    }
}
