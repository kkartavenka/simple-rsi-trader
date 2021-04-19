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
        public ParametersModel Parameters { get; set; }
        public PerformanceModel TestedPerformance { get; set; }
        public PerformanceModel TrainedPerformance { get; set; } 
        
    }
}
