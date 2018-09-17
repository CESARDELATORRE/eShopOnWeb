using CustomerSegmentation.Model;
using Microsoft.ML;
using Microsoft.ML.Trainers;
using ProductRecommendation.Train.ProductData;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.ML.Transforms;
using Microsoft.ML.Data;
using Microsoft.ML.Models;
using System;

namespace ProductRecommendation
{
    public class ModelBuilder
    {
        private readonly string productsLocation;
        private readonly string modelLocation;

        public ModelBuilder(string productsLocation, string modelLocation)
        {
            this.productsLocation = productsLocation;
            this.modelLocation = modelLocation;
        }

        public async Task BuildAndTrain()
        {
            var preProcessData = PreProcess(productsLocation);
            var learningPipeline = BuildModel(preProcessData);

            ConsoleWriteHeader("Training product forecasting model");
            var model = learningPipeline.Train<SalesData, SalesPrediction>();

            if (!string.IsNullOrEmpty(modelLocation))
            {
                ConsoleWriteHeader("Save model to local file");
                ModelHelpers.DeleteAssets(modelLocation);
                await model.WriteAsync(modelLocation);
                Console.WriteLine($"Model saved: {modelLocation}");
            }

            //var pred = model.Predict(new SalesData { CustomerId = "b0b3d87a-a904-46ac-8bd4-fd561a5c2dd3", ProductId = "1000" });

            Evaluate(preProcessData, model);
        }

        protected IEnumerable<SalesRecommendationData> PreProcess(string salesLocation)
        {
            ConsoleWriteHeader("Preprocess input file");
            Console.WriteLine($"Input file: {salesLocation}");

            var sales = SalesData.ReadFromCsv(salesLocation);

            var means = (from s in sales
                         group s by s.ProductId into gpr
                         let lookup = gpr.ToLookup(y => y.ProductId, y => y.Quantity)
                         select new {
                             ProductId = gpr.Key,
                             Mean = lookup[gpr.Key].Sum() / lookup[gpr.Key].Count()
                         }).ToDictionary(d => d.ProductId, d => d.Mean);

            var data = (from sale in sales
                    select new SalesRecommendationData
                    {
                        CustomerId = sale.CustomerId,
                        ProductId = sale.ProductId,
                        Quantity = sale.Quantity,
                        Recommendation = sale.Quantity >= means[sale.ProductId]
                    }).ToArray();

            var countFalses = data.Where(c => !c.Recommendation).Count();
            var countTrues = data.Where(c => c.Recommendation).Count();
            Console.WriteLine($"Recommendations: True={countTrues}, False={countFalses}");
            var products = sales.Select(c => c.ProductId).Distinct().Count();
            Console.WriteLine($"Unique products: {products}");
            var customers = sales.Select(c => c.CustomerId).Distinct().Count();
            Console.WriteLine($"Unique customers: {customers}");

            return data;
        }

        protected LearningPipeline BuildModel(IEnumerable<SalesRecommendationData> salesData)
        {
            ConsoleWriteHeader("Build model pipeline");

            var pipeline = new LearningPipeline();

            pipeline.Add(CollectionDataSource.Create(salesData));

            pipeline.Add(new CategoricalHashOneHotVectorizer((nameof(SalesRecommendationData.ProductId), nameof(SalesRecommendationData.ProductId) + "_OH")) { HashBits = 18 });
            pipeline.Add(new CategoricalHashOneHotVectorizer((nameof(SalesRecommendationData.CustomerId), nameof(SalesRecommendationData.CustomerId) + "_OH")) { HashBits = 18 });

            pipeline.Add(new ColumnConcatenator("Features", nameof(SalesRecommendationData.ProductId) + "_OH", nameof(SalesRecommendationData.CustomerId) + "_OH"));

            pipeline.Add(new FieldAwareFactorizationMachineBinaryClassifier() { LearningRate = 0.05F, Iters = 1, LambdaLinear = 0.0002F });

            return pipeline;
        }

        protected void Evaluate(IEnumerable<SalesRecommendationData> salesData, PredictionModel<SalesData, SalesPrediction> model)
        {
            ConsoleWriteHeader("Evaluate model");
            var testData = CollectionDataSource.Create(salesData);

            var evaluator = new BinaryClassificationEvaluator();
            var metrics = evaluator.Evaluate(model, testData);

            Console.WriteLine("Accuracy is: " + metrics.Accuracy);
            Console.WriteLine("AUC is: " + metrics.Auc);
        }

        static void ConsoleWriteHeader(params string [] lines)
        {
            var defaultColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            var maxLength = lines.Select(x => x.Length).Max();
            Console.WriteLine(" ");
            foreach (var line in lines)
            {
                Console.WriteLine(line);
            }
            Console.WriteLine(new String('#', maxLength));
            Console.ForegroundColor = defaultColor;
        }
    }
}
