using Microsoft.ML.Runtime.Api;
using Microsoft.ML.Runtime.Data;

namespace ProductRecommendation.Train.ProductData
{
    public class SalesPrediction
    {
        public string CustomerId { get; set; }

        public string ProductId { get; set; }

        [ColumnName("PredictedLabel")]
        public bool Recommendation;

        [ColumnName("Score")]
        public float Score { get; set; }

        [ColumnName("Probability")]
        public float Probability { get; set; }
    }

}
