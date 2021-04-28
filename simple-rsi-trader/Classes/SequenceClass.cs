﻿using CommonLib.Models;
using System;
using System.Linq;
using static CommonLib.Models.DataModel;

#nullable enable
namespace simple_rsi_trader.Classes
{
    public class SequenceClass
    {
        // Allow maximum 10% change over period
        private const double _maxAbsoluteChange = 0.1; 

        public SequenceClass(DataModel[] before, DataModel[]? after)
        {
            if (after != null) {
                EndPeriodClosePrice = after[^1].Data[(int)DataColumn.Close];

                FirstPeriodHighPrice = after[0].Data[(int)DataColumn.High];
                HighestPrice = after.Select(m => m.Data[(int)DataColumn.High]).Max();
                double highChange = (HighestPrice - CurrentClosePrice) / CurrentClosePrice / after.Length;

                FirstPeriodLowPrice = after[0].Data[(int)DataColumn.Low];
                LowestPrice = after.Select(m => m.Data[(int)DataColumn.Low]).Min();
                double lowChange = (LowestPrice - CurrentClosePrice) / CurrentClosePrice / after.Length;

                if (after.Length > 1) {
                    NonFirstHighestPrice = after[1..].Select(m => m.Data[(int)DataColumn.High]).Max();
                    NonFirstLowestPrice = after[1..].Select(m => m.Data[(int)DataColumn.Low]).Min();
                }

                if (highChange < 0 || highChange > _maxAbsoluteChange)
                    AllowSell = false;

                if (lowChange > 0 || lowChange < (-1) * _maxAbsoluteChange)
                    AllowBuy = false;
            }


            CurrentClosePrice = before[^1].Data[(int)DataColumn.Close];
            Id = before[^1].Id;
            Date = before[^1].Date;

            RsiSequence = before.Select(m => m.Data[(int)DataColumn.Rsi]).ToArray();
        }

        public bool AllowBuy { get; private set; }
        public bool AllowSell { get; private set; }

        public int Id { get; private set; }

        public double CurrentClosePrice { get; private set; }
        public double EndPeriodClosePrice { get; private set; }

        public DateTime Date { get; private set; }

        public double HighestPrice { get; private set; }
        public double NonFirstHighestPrice { get; private set; }
        public double FirstPeriodHighPrice { get; private set; }


        public double FirstPeriodLowPrice { get; private set; }
        public double NonFirstLowestPrice { get; private set; }
        public double LowestPrice { get; private set; }

        public double[] RsiSequence { get; private set; }
    }
}
