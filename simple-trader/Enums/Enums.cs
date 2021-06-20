using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace simple_trader.Enums
{
    public enum OptimizingParameters : int { StopLoss = 0, TakeProfit = 1, Slope = 2, RSquared = 3, Offset0 = 4, Offset1 = 5, Offset2 = 6, Offset3 = 7, RSquaredCutOff = 8, ChangeEmaCorrection = 9, RsiSlopeFitCorrection = 10 };

}
