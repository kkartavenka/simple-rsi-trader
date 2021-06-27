using CommonLib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonLib.Indicators
{
    public class MfiClass
    {
        private readonly int _period;
        public MfiClass(int period) => _period = period;

        public DataModel[] GetMfi(DataModel[] model, int columnSignalIndex, int columnVolumeIndex, int columnDestinationIndex) {
            double[] moneyFlow = new double[model.Length - 1];

            for (int i = 1; i < model.Length; i++)
                moneyFlow[i - 1] = model[i].Data[columnSignalIndex] > model[i - 1].Data[columnSignalIndex] ? model[i].Data[columnSignalIndex] * model[i].Data[columnVolumeIndex] : -1 * model[i].Data[columnSignalIndex] * model[i].Data[columnVolumeIndex];

            for (int i = moneyFlow.Length; i > _period + 1; i--) {
                double[] sequence = moneyFlow[(i - _period)..i];

                double positiveMoneyFlow = 0;
                double negativeMoneyFlow = 0;

                for (int j = 0; j < sequence.Length; j++)
                    if (sequence[j] > 0)
                        positiveMoneyFlow += sequence[j];
                    else
                        negativeMoneyFlow += Math.Abs(sequence[j]);

                double mfi = 100;

                if (negativeMoneyFlow != 0)
                    mfi = 100 - 100 / (1 + positiveMoneyFlow / negativeMoneyFlow);

                model[i].Data[columnDestinationIndex] = mfi;
            }

            return model;

        }

    }
}