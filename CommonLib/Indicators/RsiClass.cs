using CommonLib.Models;
using System;

namespace CommonLib.Indicators
{
    public class RsiClass
    {
        private readonly int _period;
        public RsiClass(int period) => _period = period;

        public DataModel[] GetRSi(DataModel[] model, int columnSignalIndex, int columnDestinationIndex)
        {
            double[] derivative = new double[model.Length - 1];

            for (int i = 1; i < model.Length; i++)
                derivative[i - 1] = model[i].Data[columnSignalIndex] - model[i - 1].Data[columnSignalIndex];

            for (int i = derivative.Length; i > _period + 1; i--)
            {
                double[] sequence = derivative[(i - _period)..i];

                double previousGain = 0;
                double previousLoss = 0;
                double currentGain = 0;
                double currentLoss = 0;

                int previousGainCount = 0;
                int previousLossCount = 0;

                for (int j = 0; j < sequence.Length - 1; j++)
                    if (sequence[j] > 0) // gain
                    {
                        previousGain += sequence[j];
                        previousGainCount++;
                    }
                    else // loss
                    {
                        previousLoss += Math.Abs(sequence[j]);
                        previousLossCount++;
                    }

                if (previousGainCount != 0)
                    previousGain = previousGain / previousGainCount;

                if (previousLossCount != 0)
                    previousLoss = previousLoss / previousLossCount;

                if (sequence[sequence.Length - 1] > 0)
                    currentGain = sequence[sequence.Length - 1];
                else
                    currentLoss = Math.Abs(sequence[sequence.Length - 1]);

                double rsi = 100;

                if (previousLoss + currentLoss != 0)
                    rsi = 100 - 100 / (1 + ((_period - 1) * previousGain + currentGain) / ((_period - 1) * previousLoss + currentLoss));

                model[i].Data[columnDestinationIndex] = rsi;
            }

            return model;
        }

    }
}
