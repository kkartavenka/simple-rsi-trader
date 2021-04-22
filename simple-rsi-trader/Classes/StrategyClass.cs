using CommonLib.Models;
using simple_rsi_trader.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static simple_rsi_trader.Classes.OperationClass;
using static simple_rsi_trader.Models.ParametersModel;

namespace simple_rsi_trader.Classes
{
    public class StrategyClass
    {
        private const double _orderDistance = 10;
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
        }

        private List<PredictionStruct> GetPredictions(Dictionary<int, SequenceClass> sequence) {
            List<PredictionStruct> preparedOrders = new();
            List<PredictionStruct> returnVar = new();

            _models.ForEach(m => {
                if (sequence[m.Parameters.RsiPeriod].CheckActivation(m.Parameters.OptimizableArray, m.Parameters)) {
                    preparedOrders.Add(sequence[m.Parameters.RsiPeriod].GetOrder(m.Parameters.OptimizableArray, m.Parameters, _roundPoint));
                }
            });

            if (_operation == OperationType.Sell) {
                preparedOrders = preparedOrders.OrderBy(m => m.LimitOrder).ThenBy(m => m.StopLoss).ToList();
                for (int i = 0; i < preparedOrders.Count; i++) {
                    if (i + 1 >= preparedOrders.Count)
                        returnVar.Add(preparedOrders[i]);
                    else {
                        if (preparedOrders[i + 1].LimitOrder - preparedOrders[i].LimitOrder > _commission * _orderDistance)
                            returnVar.Add(preparedOrders[i]);
                        else {
                            if (preparedOrders[i + 1].StopLoss < preparedOrders[i].StopLoss)
                                returnVar.Add(preparedOrders[i + 1]);
                            else
                                returnVar.Add(preparedOrders[i]);
                            i++;
                        }
                    }
                }
                returnVar = returnVar.OrderBy(m => m.LimitOrder).ToList();
            }
            else {
                preparedOrders = preparedOrders.OrderByDescending(m => m.LimitOrder).ThenByDescending(m => m.StopLoss).ToList();
                for (int i = 0; i < preparedOrders.Count; i++) {
                    if (i + 1 >= preparedOrders.Count)
                        returnVar.Add(preparedOrders[i]);
                    else {
                        if (preparedOrders[i].LimitOrder - preparedOrders[i + 1].LimitOrder > _commission * _orderDistance) {
                            returnVar.Add(preparedOrders[i]);
                        }
                        else {
                            if (preparedOrders[i + 1].StopLoss > preparedOrders[i].StopLoss)
                                returnVar.Add(preparedOrders[i + 1]);
                            else
                                returnVar.Add(preparedOrders[i]);
                            i++;
                        }
                    }
                }
                returnVar = returnVar.OrderByDescending(m => m.LimitOrder).ToList();
            }

            return returnVar;
        }

        public void LoadSequences(Dictionary<int, SequenceClass[]> testSet) => _testSet = testSet;

        public void LoadSequence(Dictionary<int, SequenceClass> predictionSet) => _predictionSet = predictionSet;

        public void PredictLast() {
            List<PredictionStruct> predictions = GetPredictions(_predictionSet);
            predictions.ForEach(row => Console.WriteLine($"Limit order: {row.LimitOrder}\tStop loss: {row.StopLoss}\tTakeProfit: {row.TakeProfit}"));
        }

        public void Test() {
            double profit = 0;
            int itemCount = _testSet.First().Value.Length;
            List<int> rsiPeriod = _testSet.Keys.ToList();

            for (int i = 0; i < itemCount; i++) {
                Dictionary<int, SequenceClass> preparedSequence = new();
                rsiPeriod.ForEach(rsiPeriod => preparedSequence.Add(rsiPeriod, _testSet[rsiPeriod][i]));

                List<PredictionStruct> predictions = GetPredictions(preparedSequence);

                SequenceClass sequence = preparedSequence.FirstOrDefault().Value;

                predictions.ForEach(prediction => {
                    OrderModel order = new(
                        close: sequence.CurrentClosePrice,
                        order: prediction.LimitOrder,
                        low: sequence.LowestPrice,
                        high: sequence.HighestPrice,
                        lowestPrice: sequence.LowestPrice,
                        highestPrice: sequence.HighestPrice);

                    var result = order.AssessProfitFromOrder(operation: _operation, stopLoss: prediction.StopLoss, takeProfit: prediction.TakeProfit, commission: _commission);
                    if (result.outcome != ActionOutcome.NoAction)
                        profit += result.profit;
                });
            }

            IsSuccess = profit > 0;
            Profit = profit;
        }

        public bool IsSuccess { get; private set; }
        public double Profit { get; private set; }
    }
}
