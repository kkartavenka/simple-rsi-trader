namespace simple_rsi_trader.Models
{
    public class SignalModel
    {
        // Filename for the data source excluding full path to the file
        public string Name { get; set; }

        // Commission associated with the operation
        public double Commission { get; set; }
    }
}
