using CommonLib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CommonLib.Models.DataModel;

namespace simple_rsi_trader.Classes
{
    public class SequenceClass
    {
        private const double _maxAbsoluteChange = 0.1;

        public SequenceClass(DataModel[] before, DataModel[]? after)
        {
            CurrentClosePrice = before[^1].Data[(int)DataColumn.Close];

            RsiSequence = before.Select(m => m.Data[(int)DataColumn.Rsi]).ToArray();

            HighPrice = after != null ? after[0].Data[(int)DataColumn.High] : 0;
            HighChange = after != null ? (HighPrice - CurrentClosePrice) / CurrentClosePrice : 0;
            HighestPrice = after != null ? after.Select(m => m.Data[(int)DataColumn.High]).Max() : 0;

            LowPrice = after != null ? after[0].Data[(int)DataColumn.Low] : 0;
            LowChange = after != null ? (LowPrice - CurrentClosePrice) / CurrentClosePrice : 0;
            LowestPrice = after != null ? after.Select(m => m.Data[(int)DataColumn.Low]).Min() : 0;


            if (HighChange < 0 || HighChange > _maxAbsoluteChange)
                AllowSell = false;

            if (LowChange > 0 || LowChange < (-1) * _maxAbsoluteChange)
                AllowBuy = false;
        }

        public bool AllowBuy { get; private set; }
        public bool AllowSell { get; private set; }

        public double CurrentClosePrice { get; private set; }

        public double HighChange { get; private set; }
        public double HighestPrice { get; private set; }
        public double HighPrice { get; private set; }

        public double LowChange { get; private set; }
        public double LowPrice { get; private set; }
        public double LowestPrice { get; private set; }

        public double[] RsiSequence { get; private set; }
    }
}
