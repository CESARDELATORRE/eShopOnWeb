﻿using Microsoft.ML.Data;
using Microsoft.ML.Runtime;
using Microsoft.ML.Runtime.Api;
using Microsoft.ML.Runtime.Data;
using Microsoft.ML.Runtime.Data.IO;
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
        public float Quantity { get; set; }

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
            var dataView = TextLoader.ReadFile(env, new TextLoader.Arguments()
            {
                Separator = "comma",
                HasHeader = true,
                Column = new[]
                {
                    new TextLoader.Column("CustomerId", DataKind.Text, 0),
                    new TextLoader.Column("ProductId", DataKind.Text, 1),
                    new TextLoader.Column("Quantity", DataKind.I4, 2)
                }
            }, new MultiFileSource(file));
            return dataView.AsEnumerable<SalesData>(env, reuseRowObject: false);
        }
    }

    public class SalesRecommendationData : SalesData
    {
        [ColumnName("Label")]
        public bool Recommendation { get; set; }

        public static void SaveToCsv(IHostEnvironment env, IEnumerable<SalesRecommendationData> salesData, string file)
        {
            var dataview = ComponentCreation.CreateDataView(env, salesData.ToList());
            using (var stream = File.OpenWrite(file))
            {
                var saver = new TextSaver(env, new TextSaver.Arguments()
                {
                    Dense = true,
                    Separator = "comma",
                    OutputHeader = true,
                    OutputSchema = true
                });
                saver.SaveData(stream, dataview, new[] { 0, 1, 2, 3 });
            }
        }
    }

    public class SalesPipelineData : SalesRecommendationData
    {
        public float[] CustomerId_OHE { get; set; }

        public float[] ProductId_OHE { get; set; }

        public float[] Features { get; set; }
    }
}
