using Microsoft.ML.Data;

namespace RecouvrementAPI.ML
{
    public class ScoringPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; }

        public float[] Score { get; set; }
    }
}
