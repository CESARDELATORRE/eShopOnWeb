using Microsoft.ML.Runtime.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ProductRecommendation.Train.ProductData
{
    public class SalesData
    {
        // CustomerId,ProductId,Units
        // 1d211951-7593-4828-9e46-1e1c56afa74a,100,12
        
        public string CustomerId { get; set; }

        public string ProductId { get; set; }

        public int Quantity { get; set; }
    }

    public class SalesRecommendationData : SalesData
    {
        [ColumnName("Label")]
        public bool Recommendation { get; set; }
    }
}
