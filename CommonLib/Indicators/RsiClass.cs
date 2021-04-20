using CommonLib.Models;
using System;

namespace CommonLib.Indicators
{
    public class RsiClass
    {
        private readonly int _period;
        public RsiClass(int period) => _period = period;

        public DataModel[] GetRSiOriginal(DataModel[] model, int columnSignalIndex, int columnDestinationIndex) {
            double[] derivative = new double[model.Length - 1];

            for (int i = 1; i < model.Length; i++)
                derivative[i - 1] = model[i].Data[columnSignalIndex] - model[i - 1].Data[columnSignalIndex];

            for (int i = derivative.Length; i > _period + 1; i--) {
                double[] sequence = derivative[(i - _period)..i];

                double gain = 0, loss = 0;

                for (int j = 0; j < sequence.Length; j++)
                    if (sequence[j] > 0)
                        gain += sequence[j];
                    else
                        loss += (-1) * sequence[j];

                gain /= _period;
                loss /= _period;

                model[i].Data[columnDestinationIndex] = 100 - (100 / (1 + gain / loss));
            }

            return model;
        }

    }
}
