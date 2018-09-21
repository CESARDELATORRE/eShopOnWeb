using Microsoft.ML.Data;
using Microsoft.ML.Runtime;
using Microsoft.ML.Runtime.Api;
using Microsoft.ML.Runtime.Data;
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


        public static IEnumerable<SalesData> ReadFromCsv(IHostEnvironment env, string file)
        {
            var reader = TextLoader.CreateReader<SalesData>(env, ctx => new SalesData
            {
                CustomerId = ctx.LoadText(1).ToString(),
                ProductId = ctx.LoadText(2).ToString(),
                Quantity = int.Parse(ctx.LoadDouble(3).ToString())
            });

            return reader.Read(new MultiFileSource(file)).AsDynamic.AsEnumerable<SalesData>(env, false);
        }
    }

    public class SalesRecommendationData : SalesData
    {
        [ColumnName("Label")]
        public bool Recommendation { get; set; }
    }

    public class SalesPipelineData : SalesRecommendationData
    {
        public float[] CustomerId_OHE { get; set; }

        public float[] ProductId_OHE { get; set; }

        public float[] Features { get; set; }
    }
}
