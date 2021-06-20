namespace simple_rsi_trader.Models
{
    public struct ActivationReturnStruct
    {
        public ActivationReturnStruct(bool activated, double slope, double rSquared) {
            Activated = activated;
            RSquared = rSquared;
            Slope = slope;
        }

        public bool Activated { get; }
        public double Slope { get; }
        public double RSquared { get; }
    }
}
