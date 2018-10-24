using CustomerSegmentation.Model;
using ProductRecommendation.Train.ProductData;
using System.Collections.Generic;
using System.Linq;
using System;
using static CustomerSegmentation.Model.ConsoleHelpers;
using Microsoft.ML.Runtime.Data;
using Microsoft.ML.Runtime.Api;
using Microsoft.ML.Runtime.FactorizationMachine;
using System.IO;
using Microsoft.ML.Trainers;
using Microsoft.ML;

namespace ProductRecommendation
{
    public class ModelBuilder
    {
        private readonly string orderItemsLocation;
        private readonly string modelLocation;
        private readonly LocalEnvironment env;

        public ModelBuilder(string orderItemsLocation, string modelLocation)
        {
            this.orderItemsLocation = orderItemsLocation;
            this.modelLocation = modelLocation;
            env = new LocalEnvironment(seed:1);  //Seed set to any number so you have a deterministic environment
        }

        public IEnumerable<SalesRecommendationData> PreProcess(string orderItemsLocation)
        {
            ConsoleWriteHeader("Preprocess input file");
            Console.WriteLine($"Input file: {orderItemsLocation}");

            var sales = SalesData.ReadFromCsv(env, orderItemsLocation).ToArray();

            var agg = (from s in sales
                       group s by (s.CustomerId, s.ProductId) into gpr
                       select new { gpr.Key.CustomerId, gpr.Key.ProductId, Quantity = gpr.Sum(s => s.Quantity) });

            // Calculate the mean Quantiy sold of each product
            // This value will be used as a threshold for discretizing the Quantity value
            var means = (from s in agg
                         group s by s.ProductId into gpr
                         let lookup = gpr.ToLookup(y => y.ProductId, y => y.Quantity)
                         select new {
                             ProductId = gpr.Key,
                             Mean = lookup[gpr.Key].Sum() / lookup[gpr.Key].Count()
                         }).ToDictionary(d => d.ProductId, d => d.Mean);

            // Add a new Recommendation column based on the Quantity column
            var data = (from sale in agg
                    select new SalesRecommendationData
                    {
                        CustomerId = sale.CustomerId,
                        ProductId = sale.ProductId,
                        Quantity = sale.Quantity,
                        Recommendation = sale.Quantity >= means[sale.ProductId]
                    }).ToArray();

            // Quick facts about the dataset
            var countFalses = data.Where(c => !c.Recommendation).Count();
            var countTrues = data.Where(c => c.Recommendation).Count();
            Console.WriteLine($"Recommendations: True={countTrues}, False={countFalses}");
            var products = sales.Select(c => c.ProductId).Distinct().Count();
            Console.WriteLine($"Unique products: {products}");
            var customers = sales.Select(c => c.CustomerId).Distinct().Count();
            Console.WriteLine($"Unique customers: {customers}");

            var salesPrepLocation = Path.Combine(Path.GetDirectoryName(orderItemsLocation), "orderItemsPre.csv");
            Console.WriteLine($"Output file: {salesPrepLocation}");

            SalesRecommendationData.SaveToCsv(env, data, salesPrepLocation);

            return data;
        }

        public void BuildAndTrainStaticApi()
        {
            ConsoleWriteHeader("Build and Train using Static API");
            Console.Out.WriteLine($"Input file: {orderItemsLocation}");

            ConsoleWriteHeader("Reading file ...");
            var reader = TextLoader.CreateReader(env,
                            c => (
                                CustomerId: c.LoadText(0),
                                ProductId: c.LoadText(1),
                                Quantity: c.LoadFloat(2),
                                Label: c.LoadBool(3)),
                            separator: ',', hasHeader: true);

            FieldAwareFactorizationMachinePredictor pred = null;
            var ctx = new BinaryClassificationContext(env);

            var est = reader.MakeNewEstimator()
                .Append(row => (CustomerId_OHE: row.CustomerId.OneHotEncoding(), ProductId_OHE: row.ProductId.OneHotEncoding(), row.Label))
                .Append(row => (Features: row.CustomerId_OHE.ConcatWith(row.ProductId_OHE), row.Label))
                .Append(row => (row.Label,
                preds: ctx.Trainers.FieldAwareFactorizationMachine(
                    row.Label,
                    new[] { row.Features },
                    advancedSettings: ffmArguments => ffmArguments.Shuffle = false,
                    onFit: p => pred = p)));

            ConsoleWriteHeader("Training model for recommendations");
            var dataSource = reader.Read(new MultiFileSource(orderItemsLocation));
            var model = est.Fit(dataSource);

            // inspect data
            var data = model.Transform(dataSource);
            var trainData = data.AsDynamic;
            var columnNames = trainData.Schema.GetColumnNames().ToArray();
            var trainDataAsEnumerable = trainData.AsEnumerable<SalesPipelineData>(env, false).Take(10).ToArray();

            ConsoleWriteHeader("Evaluate model");
            var metrics = ctx.Evaluate(data, r => r.Label, r => r.preds);
            Console.WriteLine($"Accuracy is: {metrics.Accuracy}");
            Console.WriteLine($"AUC is: {metrics.Auc}");

            ConsoleWriteHeader("Save model to local file");
            ModelHelpers.DeleteAssets(modelLocation);
            using (var f = new FileStream(modelLocation, FileMode.Create))
                model.AsDynamic.SaveTo(env, f);
            Console.WriteLine($"Model saved: {modelLocation}");
        }

        public void BuildAndTrainDynamicApi()
        {
            ConsoleWriteHeader("Build and Train using Static API");
            Console.Out.WriteLine($"Input file: {orderItemsLocation}");

            ConsoleWriteHeader("Reading file ...");

            var reader = new TextLoader(env, new TextLoader.Arguments
            {
                Column = new[] {
                    new TextLoader.Column("CustomerId", DataKind.Text, 0 ),
                    new TextLoader.Column("ProductId", DataKind.Text, 1 ),
                    new TextLoader.Column("Quantity", DataKind.R4, 2 ),
                    new TextLoader.Column("Label", DataKind.Bool, 3 )
                },
                HasHeader = true,
                Separator = ","
            });

            var estimator = new CategoricalEstimator(env, new[] {
                            new CategoricalEstimator.ColumnInfo("CustomerId", "CustomerId_OHE", CategoricalTransform.OutputKind.Ind),
                            new CategoricalEstimator.ColumnInfo("ProductId", "ProductId_OHE", CategoricalTransform.OutputKind.Ind) })
                .Append(new ConcatEstimator(env, "Features", new[] { "ProductId_OHE", "CustomerId_OHE" }))
                .Append(new FieldAwareFactorizationMachineTrainer(env, "Label", new[] { "Features" }, advancedSettings: p =>p.Shuffle = false));

            var ctx = new BinaryClassificationContext(env);

            ConsoleWriteHeader("Training model for recommendations");
            var dataSource = reader.Read(new MultiFileSource(orderItemsLocation));
            var model = estimator.Fit(dataSource);

            // inspect data
            var data = model.Transform(dataSource);
            var columnNames = data.Schema.GetColumnNames().ToArray();
            var trainDataAsEnumerable = data.AsEnumerable<SalesPipelineData>(env, false).Take(10).ToArray();

            ConsoleWriteHeader("Evaluate model");
            var metrics = ctx.Evaluate(data, "Label");
            Console.WriteLine($"Accuracy is: {metrics.Accuracy}");
            Console.WriteLine($"AUC is: {metrics.Auc}");

            ConsoleWriteHeader("Save model to local file");
            ModelHelpers.DeleteAssets(modelLocation);
            using (var f = new FileStream(modelLocation, FileMode.Create))
                model.SaveTo(env, f);
            Console.WriteLine($"Model saved: {modelLocation}");
        }
    }
}
