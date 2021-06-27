namespace simple_trader.Enums
{
    public enum OptimizingParameters : int { 
        StopLoss = 0, 
        TakeProfit = 1, 

        Slope = 2, 
        RSquared = 3, 
        
        Offset0 = 4, 
        Offset1 = 5, 
        Offset2 = 6, 
        
        Mfi0 = 7,
        Mfi1 = 8,
        //Mfi2 = 9,

        Rsi0 = 9,
        Rsi1 = 10,
        //Rsi2 = 12,

        ChangeEmaCorrection = 11,
        SlopeRSquaredFitCorrection = 12
    };

}
