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
        
        [Column("1")]
        public string CustomerId { get; set; }

        [Column("2")]
        public string ProductId { get; set; }

        [Column("3")]
        public int Quantity { get; set; }

        public static IEnumerable<SalesData> ReadFromCsv(string file)
        {
            return File.ReadAllLines(file)
                .Skip(1) // skip header
                .Select(x => x.Split(','))
                .Select(x => new SalesData()
                {
                    CustomerId = x[0],
                    ProductId = x[1],
                    Quantity = int.Parse(x[2])
                });
        }
    }

    public class SalesRecommendationData : SalesData
    {
        [ColumnName("Label")]
        public bool Recommendation { get; set; }
    }
}
