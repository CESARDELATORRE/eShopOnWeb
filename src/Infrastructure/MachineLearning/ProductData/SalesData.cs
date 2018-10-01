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

        public float Quantity { get; set; }

        public static IEnumerable<SalesData> ReadFromCsv(string file)
        {
            return File.ReadAllLines(file)
                .Skip(8) // skip header
                .Select(x => x.Split(','))
                .Select(x => new SalesData()
                {
                    CustomerId = x[0],
                    ProductId = x[1],
                    Quantity = int.Parse(x[2])
                });
        }
    }
}
