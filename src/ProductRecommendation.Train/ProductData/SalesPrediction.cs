using Microsoft.ML.Runtime.Api;
using Microsoft.ML.Runtime.Data;

namespace ProductRecommendation.Train.ProductData
{
    public class SalesPrediction
    {
        public string CustomerId { get; set; }

        public string ProductId { get; set; }

        [ColumnName("PredictedLabel")]
        public DvBool Recommendation;

        [ColumnName("Score")]
        public float Score { get; set; }
    }

}
