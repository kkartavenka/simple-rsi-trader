using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonLib
{
    public class CsvReaderClass
    {
        public CsvReaderClass() { }
        public CsvReaderClass(string fileName, char splitChar, DateTime restrictDate)
        {
            string[] lines = File.ReadAllLines(fileName);
            List<int> roundPoints = new List<int>();

            foreach (string line in lines)
            {
                string[] row = line.Split(splitChar);

                DateTime trueDate;

                bool v1Parsed = DateTime.TryParseExact($"{row[0]} {row[1]}", "yyyy.MM.dd H:mm", null, System.Globalization.DateTimeStyles.None, out DateTime v1);
                bool v2Parsed = DateTime.TryParseExact($"{row[0]} {row[1]}", "yyyy.MM.dd HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime v2);

                trueDate = v1Parsed ? v1 : v2Parsed ? v2 : new DateTime();

                if (trueDate != new DateTime())
                {
                    OhlcModel newElement = new OhlcModel()
                    {
                        Date = trueDate,
                        Close = Convert.ToSingle(row[5]),
                        High = Convert.ToSingle(row[3]),
                        Open = Convert.ToSingle(row[2]),
                        Low = Convert.ToSingle(row[4]),
                        Volume = Convert.ToSingle(row[6])
                    };

                    newElement.TypicalPrice = (newElement.Close + newElement.High + newElement.Low) / 3;

                    Ohlc.Add(newElement);
                    try
                    {
                        roundPoints.Add(row[5].Split('.').ElementAt(1).Length);
                    }
                    catch
                    { }
                }
                else
                    Console.WriteLine("Error parsing date");
            }

            Ohlc = Ohlc
                .OrderBy(m => m.Date)
                .Where(m => m.Date > restrictDate)
                .ToList();

            RoundPoint = (int)roundPoints.ToArray().Median();
        }

        public async Task LoadFileAsync(string fileName, char splitChar, DateTime restrictDate)
        {
            string[] lines = await File.ReadAllLinesAsync(fileName);
            ReadFile(lines, splitChar, restrictDate);
        }

        private void ReadFile(string[] lines, char splitChar, DateTime restrictDate)
        {
            //int[] roundPoints = new int[lines.Length];
            OhlcModel[] dataArray = new OhlcModel[lines.Length];

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string[] row = line.Split(splitChar);

                DateTime trueDate;

                bool v1Parsed = DateTime.TryParseExact($"{row[0]} {row[1]}", "yyyy.MM.dd H:mm", null, System.Globalization.DateTimeStyles.None, out DateTime v1);
                bool v2Parsed = DateTime.TryParseExact($"{row[0]} {row[1]}", "yyyy.MM.dd HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime v2);

                trueDate = v1Parsed ? v1 : v2Parsed ? v2 : new DateTime();

                if (trueDate != new DateTime())
                {
                    dataArray[i] = new OhlcModel()
                    {
                        Date = trueDate,
                        Close = Convert.ToSingle(row[5]),
                        High = Convert.ToSingle(row[3]),
                        Open = Convert.ToSingle(row[2]),
                        Low = Convert.ToSingle(row[4]),
                        Volume = Convert.ToSingle(row[6])
                    };

                    dataArray[i].TypicalPrice = (dataArray[i].Close + dataArray[i].High + dataArray[i].Low) / 3;

                    try
                    {
                        int roundPoint = row[5].IndexOf('.') >= 0 ? row[5].Length - row[5].IndexOf('.') - 1 : 0;
                        if (roundPoint > RoundPoint)
                            RoundPoint = roundPoint;
                    }
                    catch
                    { }
                }
                else
                    Console.WriteLine("Error parsing date");
            }

            Ohlc = dataArray.OrderBy(m => m.Date).Where(m => m.Date > restrictDate).ToList();
        }

        public void LoadFile(string fileName, char splitChar, DateTime restrictDate)
        {
            string[] lines = File.ReadAllLines(fileName);
            ReadFile(lines, splitChar, restrictDate);
        }


        public void PrepareSourceData(int testSize, int? trainSize, int dataArraySize, bool cleanUp, int cleanUpWindowSize = 20)
        {
            int i = cleanUpWindowSize;
            while (cleanUp && i < Ohlc.Count())
            {
                List<OhlcModel> selectedSequence = Ohlc.Skip(i - cleanUpWindowSize).Take(cleanUpWindowSize).ToList();
                double median = selectedSequence.Select(m => (double)m.Volume).SkipLast(1).ToArray().Median();

                if (Ohlc[i - 1].Volume <= median * 0.15)
                {
                    if (i > 2 && i < Ohlc.Count())
                    {
                        int dayBefore = (int)Math.Abs(Math.Round((Ohlc[i - 2].Date - Ohlc[i - 1].Date).TotalDays));
                        int dayAfter = (int)Math.Abs(Math.Round((Ohlc[i].Date - Ohlc[i - 1].Date).TotalDays));

                        if (dayAfter >= 2 && dayBefore == 1)
                        {
                            Ohlc[i - 2].Volume += Ohlc[i - 1].Volume;
                            Ohlc[i - 2].Close = Ohlc[i - 1].Close;
                            Ohlc[i - 2].High = Math.Max(Ohlc[i - 1].High, Ohlc[i - 2].High);
                            Ohlc[i - 2].Low = Math.Min(Ohlc[i - 1].Low, Ohlc[i - 2].Low);
                            Ohlc.RemoveAt(i - 1);
                        }
                        else if (dayBefore >= 2 && dayAfter == 1)
                        {
                            Ohlc[i].Volume += Ohlc[i - 1].Volume;
                            Ohlc[i].Open = Ohlc[i - 1].Open;
                            Ohlc[i].High = Math.Max(Ohlc[i - 1].High, Ohlc[i].High);
                            Ohlc[i].Low = Math.Min(Ohlc[i - 1].Low, Ohlc[i].Low);

                            Ohlc.RemoveAt(i - 1);
                        }
                        else if (Ohlc[i - 1].Date.Day == 1 && Ohlc[i - 1].Date.Month == 1)
                            Ohlc.RemoveAt(i - 1);
                        else
                            i++;
                    }
                }
                else
                    i++;
            }

            ContentTestOhlc = Ohlc.Copy().TakeLast(testSize + 400).ToList();
            if (trainSize != null)
                ContentTrainOhlc = Ohlc.Copy().SkipLast(testSize).TakeLast((int)trainSize + 400).ToList();
            else
                ContentTrainOhlc = Ohlc.Copy().SkipLast(testSize).ToList();

            ContentTest = Translate(ohlcData: ContentTestOhlc, dataArraySize: dataArraySize); //new DataModel[ContentTestOhlc.Count()];
            ContentTrain = Translate(ohlcData: ContentTrainOhlc, dataArraySize: dataArraySize);// new DataModel[ContentTrainOhlc.Count()];
            ContentAll = Translate(ohlcData: Ohlc.Copy(), dataArraySize: dataArraySize);
        }

        private DataModel[] Translate(List<OhlcModel> ohlcData, int dataArraySize)
        {
            DataModel[] returnModel = new DataModel[ohlcData.Count()];

            for (int j = 0; j < ohlcData.Count(); j++)
            {
                double[] dataArray = new double[dataArraySize];
                dataArray[0] = ohlcData[j].Open;
                dataArray[1] = ohlcData[j].High;
                dataArray[2] = ohlcData[j].Low;
                dataArray[3] = ohlcData[j].Close;
                dataArray[4] = ohlcData[j].TypicalPrice;
                dataArray[5] = ohlcData[j].Volume;

                returnModel[j] = new DataModel()
                {
                    Date = ohlcData[j].Date,
                    Data = dataArray
                };
            }

            return returnModel;
        }


        #region Properties

        public DataModel[] ContentAll { get; private set; }
        public DataModel[] ContentTest { get; private set; }
        public List<OhlcModel> ContentTestOhlc { get; private set; }

        public DataModel[] ContentTrain { get; private set; }
        public List<OhlcModel> ContentTrainOhlc { get; private set; }

        //public List<OhlcModel> Ohlc { get; private set; } = new List<OhlcModel>();
        public List<OhlcModel> Ohlc { get; private set; } = new List<OhlcModel>();

        public int RoundPoint { get; private set; }

        #endregion
    }

}
