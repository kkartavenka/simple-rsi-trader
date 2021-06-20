using CommonLib.Enums;
using simple_trader.Models;
using System.Collections.Generic;
using System.Linq;

namespace simple_trader.Classes
{
    public static class SavedModelExtension
    {
        public static List<SavedModel> SelectDistinct(this IGrouping<OperationType, SavedModel> values) {

            List<SavedModel> returnVar = new();

            values.GroupBy(m => new { m.Parameters.SequenceLength }).ToList().ForEach(row => {
                IEnumerable<SavedModel> orderedGroup = row.OrderBy(m => m.TrainedPerformance.Profit).ThenBy(m => m.TestedPerformance.Profit);
                int itemCount = orderedGroup.Count();

                for (int i = 0; i < itemCount;) {
                    double profitPoint = orderedGroup.ElementAt(i).TrainedPerformance.Profit;
                    double range = profitPoint * 1.01;

                    IEnumerable<SavedModel> simpleCluster = orderedGroup.Where(m => m.TrainedPerformance.Profit >= profitPoint && m.TrainedPerformance.Profit <= range);
                    returnVar.Add(simpleCluster.OrderByDescending(m => m.TestedPerformance.Profit / m.TestedPerformance.LossRate).First());

                    i += simpleCluster.Count();
                }
            });

            return returnVar;
        }
    }
}