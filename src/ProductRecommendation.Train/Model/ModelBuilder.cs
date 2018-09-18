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
using static CustomerSegmentation.Model.ModelHelpers;

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

            PredictionModel<SalesData, SalesPrediction> model = TrainModel(learningPipeline);

            if (!string.IsNullOrEmpty(modelLocation))
            {
                await SaveModel(model);
            }

            //var pred = model.Predict(new SalesData { CustomerId = "b0b3d87a-a904-46ac-8bd4-fd561a5c2dd3", ProductId = "1000" });

            EvaluateModel(preProcessData, model);
        }

        private async Task SaveModel(PredictionModel<SalesData, SalesPrediction> model)
        {
            ConsoleWriteHeader("Save model to local file");
            ModelHelpers.DeleteAssets(modelLocation);
            await model.WriteAsync(modelLocation);
            Console.WriteLine($"Model saved: {modelLocation}");
        }

        private static PredictionModel<SalesData, SalesPrediction> TrainModel(LearningPipeline learningPipeline)
        {
            ConsoleWriteHeader("Training recommendation model");
            return learningPipeline.Train<SalesData, SalesPrediction>();
        }

        protected IEnumerable<SalesRecommendationData> PreProcess(string salesLocation)
        {
            ConsoleWriteHeader("Preprocess input file");
            Console.WriteLine($"Input file: {salesLocation}");

            var sales = SalesData.ReadFromCsv(salesLocation);

            // Calculate the mean Quantiy sold of each product
            // This value will be used as a threshold for discretizing the Quantity value
            var means = (from s in sales
                         group s by s.ProductId into gpr
                         let lookup = gpr.ToLookup(y => y.ProductId, y => y.Quantity)
                         select new {
                             ProductId = gpr.Key,
                             Mean = lookup[gpr.Key].Sum() / lookup[gpr.Key].Count()
                         }).ToDictionary(d => d.ProductId, d => d.Mean);

            // Add a new Recommendation column based on the Quantity column
            var data = (from sale in sales
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

            return data;
        }

        protected LearningPipeline BuildModel(IEnumerable<SalesRecommendationData> salesData)
        {
            ConsoleWriteHeader("Build model pipeline");

            var pipeline = new LearningPipeline();

            pipeline.Add(CollectionDataSource.Create(salesData));

            // One Hot Encoding using Hash Vector. The new columns are named as the original ones, but adding the suffix "_OH"
            pipeline.Add(new CategoricalHashOneHotVectorizer((nameof(SalesRecommendationData.ProductId), nameof(SalesRecommendationData.ProductId) + "_OH")) { HashBits = 18 });
            pipeline.Add(new CategoricalHashOneHotVectorizer((nameof(SalesRecommendationData.CustomerId), nameof(SalesRecommendationData.CustomerId) + "_OH")) { HashBits = 18 });

            // Combine *_OH columns into Features
            pipeline.Add(new ColumnConcatenator("Features", nameof(SalesRecommendationData.ProductId) + "_OH", nameof(SalesRecommendationData.CustomerId) + "_OH"));

            // Adds a binary classifier learner, using the Field Factorization Machines based on libFFM 
            pipeline.Add(new FieldAwareFactorizationMachineBinaryClassifier());

            return pipeline;
        }

        protected void EvaluateModel(IEnumerable<SalesRecommendationData> salesData, PredictionModel<SalesData, SalesPrediction> model)
        {
            ConsoleWriteHeader("Evaluate model");
            var testData = CollectionDataSource.Create(salesData);

            var evaluator = new BinaryClassificationEvaluator();
            var metrics = evaluator.Evaluate(model, testData);

            // These metrics are overstimated as we are using for evaluation the same training dataset 
            Console.WriteLine("Accuracy is: " + metrics.Accuracy);
            Console.WriteLine("AUC is: " + metrics.Auc);
        }
    }
}
