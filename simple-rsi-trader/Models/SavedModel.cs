using CommonLib.Models;

namespace simple_rsi_trader.Models
{
    public class SavedModel
    {
        public SavedModel(ParametersModel parameters, PerformanceModel tested, PerformanceModel trained) {
            Parameters = parameters;
            TestedPerformance = tested;
            TrainedPerformance = trained;
        }
        public ParametersModel Parameters { get; private set; }
        public PerformanceModel TestedPerformance { get; private set; }
        public PerformanceModel TrainedPerformance { get; private set; } 
        
    }
}
