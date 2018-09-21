using CustomerSegmentation.Model;
using Microsoft.ML.Legacy;
using Microsoft.ML.Legacy.Trainers;
using ProductRecommendation.Train.ProductData;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.ML.Legacy.Transforms;
using Microsoft.ML.Legacy.Data;
using Microsoft.ML.Legacy.Models;
using System;
using static CustomerSegmentation.Model.ModelHelpers;
using Microsoft.ML.Runtime.Data;
using Microsoft.ML.Runtime.Api;
using Microsoft.ML.Runtime.FactorizationMachine;
using Microsoft.ML.Core.Data;
using System.IO;
using Microsoft.ML.Transforms;

namespace ProductRecommendation
{
    public class ModelBuilder
    {
        private readonly string productsLocation;
        private readonly string modelLocation;
        private readonly ConsoleEnvironment env;

        public ModelBuilder(string productsLocation, string modelLocation)
        {
            this.productsLocation = productsLocation;
            this.modelLocation = modelLocation;
            env = new ConsoleEnvironment(42);
        }

        public void BuildAndTrain()
        {
            var preProcessData = PreProcess(productsLocation);

            var (trainData, pipe) = BuildModel(preProcessData);

            TransformerChain<ITransformer> model = TrainModel(pipe, trainData);

            if (!string.IsNullOrEmpty(modelLocation))
            {
                SaveModel(model, modelLocation);
            }

            //var pred = model.Predict(new SalesData { CustomerId = "b0b3d87a-a904-46ac-8bd4-fd561a5c2dd3", ProductId = "1000" });

            //EvaluateModel(preProcessData, model);
        }

        public void Test()
        {
            var loadedModel = LoadModel(modelLocation);
            var predictions = PredictDataUsingModel(productsLocation, loadedModel).ToArray();
        }

        private void SaveModel(TransformerChain<ITransformer> model, string modelLocation)
        {
            ConsoleWriteHeader("Save model to local file");
            ModelHelpers.DeleteAssets(modelLocation);
            //await model.WriteAsync(modelLocation);
            using (var fs = File.Create(modelLocation))
                model.SaveTo(env, fs);
            Console.WriteLine($"Model saved: {modelLocation}");
        }

        private static TransformerChain<ITransformer> TrainModel(EstimatorChain<ITransformer> pipe, IDataView data)
        {
            ConsoleWriteHeader("Training recommendation model");
            return pipe.Fit(data);
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

        protected (IDataView, EstimatorChain<ITransformer>) BuildModel(IEnumerable<SalesRecommendationData> salesData)
        {
            ConsoleWriteHeader("Build model pipeline");

            const string customerColumn = nameof(SalesRecommendationData.CustomerId);
            const string customerColumnOneHotEnc = customerColumn + "_OHE";
            const string productColumn = nameof(SalesRecommendationData.ProductId);
            const string productColumnOneHotEnc = productColumn + "_OHE";
            //const string featuresColumn = "Features";
            const string labelColumn = "Label";

            var dataview = ComponentCreation.CreateDataView(env, salesData.ToList());

            var pipe = new CategoricalEstimator(env, new[] {
                new CategoricalEstimator.ColumnInfo(productColumn, productColumnOneHotEnc, CategoricalTransform.OutputKind.Ind),
                new CategoricalEstimator.ColumnInfo(customerColumn, customerColumnOneHotEnc, CategoricalTransform.OutputKind.Ind),
            });

            IEstimator<ITransformer> est = new FieldAwareFactorizationMachineTrainer(env, labelColumn, new[] { customerColumnOneHotEnc, productColumnOneHotEnc },
                advancedSettings: s =>
                {
                    s.Shuffle = true;
                    s.Iters = 3;
                    s.Caching = Microsoft.ML.Runtime.EntryPoints.CachingOptions.Memory;
                });

            //var pipe = new HashEstimator(env, new[] {
            //    new HashTransformer.ColumnInfo(productColumn, productColumnOneHotEnc),
            //    new HashTransformer.ColumnInfo(customerColumn, customerColumnOneHotEnc)
            //});

            // inspect data
            var trainData = pipe.Fit(dataview).Transform(dataview);
            var columnNames = trainData.Schema.GetColumnNames().ToArray();
            var trainDataAsEnumerable = trainData.AsEnumerable<SalesPipelineData>(env, false).Take(10).ToArray();

            return (dataview, pipe.Append(est));

            //var trainData = pipe.Fit(dataview).Transform(dataview);

            //var concat = new ConcatTransform(env, 
            //    new ConcatTransform.ColumnInfo(featuresColumn, customerColumnOneHotEnc, productColumnOneHotEnc)
            //    );

            //var data = concat.Transform(pipeline.Fit(dataview).Transform(dataview));

            //var trainRoles = new RoleMappedData(data, label: "Label", feature: featuresColumn);
            //var trainer = new FieldAwareFactorizationMachineTrainer(env, new FieldAwareFactorizationMachineTrainer.Arguments());
            //var model = trainer.Train(new Microsoft.ML.Runtime.TrainContext(trainRoles));

            //return (trainData, est);
            //var trainTransformer = est.Fit(trainData);

            //model.Save(new Microsoft.ML.Runtime.Model.ModelSaveContext())


            //IDataScorerTransform scorer = ScoreUtils.GetScorer(model, trainRoles, env, trainRoles.Schema);

            // Create prediction engine and test predictions.
            //var predictor = env.CreatePredictionEngine<SalesData, SalesPrediction>(scorer);


            //pipe.

            //var pipeline = new LearningPipeline();

            //pipeline.Add(CollectionDataSource.Create(salesData));

            //// One Hot Encoding using Hash Vector. The new columns are named as the original ones, but adding the suffix "_OH"
            //pipeline.Add(new CategoricalHashOneHotVectorizer((nameof(SalesRecommendationData.ProductId), nameof(SalesRecommendationData.ProductId) + "_OH")) { HashBits = 18 });
            //pipeline.Add(new CategoricalHashOneHotVectorizer((nameof(SalesRecommendationData.CustomerId), nameof(SalesRecommendationData.CustomerId) + "_OH")) { HashBits = 18 });

            //// Combine *_OH columns into Features
            //pipeline.Add(new ColumnConcatenator("Features", nameof(SalesRecommendationData.ProductId) + "_OH", nameof(SalesRecommendationData.CustomerId) + "_OH"));

            //// Adds a binary classifier learner, using the Field Factorization Machines based on libFFM 
            //pipeline.Add(new FieldAwareFactorizationMachineBinaryClassifier());

            //return pipeline;
        }

        protected PredictionFunction<SalesData, SalesPrediction> LoadModel(string modelLocation)
        {
            using (var file = File.OpenRead(modelLocation))
            {
                return TransformerChain
                    .LoadFrom(env, file)
                    .MakePredictionFunction<SalesData, SalesPrediction>(env);
            }
        }

        protected IEnumerable<SalesPrediction> PredictDataUsingModel(string testFileLocation, PredictionFunction<SalesData, SalesPrediction> model)
        {
            var testData = SalesData.ReadFromCsv(testFileLocation);
            foreach (var item in testData)
            {
                yield return model.Predict(item);
            }
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
