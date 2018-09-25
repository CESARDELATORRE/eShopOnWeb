using CustomerSegmentation.Model;
using ProductRecommendation.Train.ProductData;
using System.Collections.Generic;
using System.Linq;
using System;
using static CustomerSegmentation.Model.ModelHelpers;
using Microsoft.ML.Runtime.Data;
using Microsoft.ML.Runtime.Api;
using Microsoft.ML.Runtime.FactorizationMachine;
using Microsoft.ML.Core.Data;
using System.IO;
using Microsoft.ML.Runtime;
using Microsoft.ML.Runtime.Training;
using Microsoft.ML.Trainers;

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

        public void BuildAndTrainEstimatorAPI()
        {
            var (trainData, pipe) = BuildModel(orderItemsLocation);

            TransformerChain<ITransformer> model = TrainModel(pipe, trainData);

            if (!string.IsNullOrEmpty(modelLocation))
            {
                SaveModel(model, modelLocation);
            }

            // REVIEW!!
            // metrics do not work (always zero)
            //EvaluateModel(model.Transform(trainData));
        }

        public void Test()
        {
            var loadedModel = LoadModel(modelLocation);
            var predictions = PredictDataUsingModel(orderItemsLocation, loadedModel).ToArray();
        }

        private void SaveModel(TransformerChain<ITransformer> model, string modelLocation)
        {
            ConsoleWriteHeader("Save model to local file");
            ModelHelpers.DeleteAssets(modelLocation);
            using (var fs = File.Create(modelLocation))
                model.SaveTo(env, fs);
            Console.WriteLine($"Model saved: {modelLocation}");
        }

        private static TransformerChain<ITransformer> TrainModel(EstimatorChain<ITransformer> pipe, IDataView dataView)
        {
            ConsoleWriteHeader("Training recommendation model");
            return pipe.Fit(dataView);
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

        protected (IDataView, EstimatorChain<ITransformer>) BuildModel(string orderItemsLocation)
        {
            ConsoleWriteHeader("Build model pipeline");

            const string customerColumn = nameof(SalesRecommendationData.CustomerId);
            const string customerColumnOneHotEnc = customerColumn + "_OHE";
            const string productColumn = nameof(SalesRecommendationData.ProductId);
            const string productColumnOneHotEnc = productColumn + "_OHE";
            const string featuresColumn = DefaultColumnNames.Features;
            const string labelColumn = DefaultColumnNames.Label;

            var reader = TextLoader.CreateReader(env,
                            c => (
                                CustomerId: c.LoadText(0),
                                ProductId: c.LoadText(1),
                                Quantity: c.LoadFloat(2),
                                Label: c.LoadBool(3)),
                            separator: ',', hasHeader: true);

            var pipe = new CategoricalEstimator(env, new[] {
                new CategoricalEstimator.ColumnInfo(productColumn, productColumnOneHotEnc, CategoricalTransform.OutputKind.Ind),
                new CategoricalEstimator.ColumnInfo(customerColumn, customerColumnOneHotEnc, CategoricalTransform.OutputKind.Ind),
            }).Append(new ConcatEstimator(env, featuresColumn, productColumnOneHotEnc, customerColumnOneHotEnc));

            IEstimator<ITransformer> est = new FieldAwareFactorizationMachineTrainer(env, labelColumn, new[] { featuresColumn },
                advancedSettings: s =>
                {
                    s.Shuffle = false;
                    s.Iters = 3;
                    s.Caching = Microsoft.ML.Runtime.EntryPoints.CachingOptions.Memory;
                });

            var dataview = reader.Read(new MultiFileSource(orderItemsLocation)).AsDynamic;

            // inspect data
            var trainData = pipe.Fit(dataview).Transform(dataview);
            var columnNames = trainData.Schema.GetColumnNames().ToArray();
            var trainDataAsEnumerable = trainData.AsEnumerable<SalesPipelineData>(env, false).Take(10).ToArray();

            return (dataview, pipe.Append(est));
        }

        public void BuildAndTrainStaticApi()
        {
            ConsoleWriteHeader("Build and Train using Static API");
            Console.Out.WriteLine($"Input file: {orderItemsLocation}");

            var ctx = new BinaryClassificationContext(env);

            ConsoleWriteHeader("Reading file ...");
            var reader = TextLoader.CreateReader(env,
                            c => (
                                CustomerId: c.LoadText(0),
                                ProductId: c.LoadText(1),
                                Quantity: c.LoadFloat(2),
                                Label: c.LoadBool(3)),
                            separator: ',', hasHeader: true);

            FieldAwareFactorizationMachinePredictor pred = null;

            var est = reader.MakeNewEstimator()
                .Append(row => (CustomerId_OHE: row.CustomerId.OneHotEncoding(), ProductId_OHE: row.ProductId.OneHotEncoding(), row.Label))
                .Append(row => (Features: row.CustomerId_OHE.ConcatWith(row.ProductId_OHE), row.Label))
                .Append(row => (row.Label, 
                preds: ctx.Trainers.FieldAwareFactorizationMachine(
                    row.Label, 
                    new[] { row.Features }, 
                    advancedSettings: ffmArguments => ffmArguments.Shuffle = false,
                    onFit: p => pred = p)));

            var pipe = reader.Append(est);

            ConsoleWriteHeader("Training model for recommendations");
            var dataSource = new MultiFileSource(orderItemsLocation);
            var model = pipe.Fit(dataSource);

            var data = model.Read(dataSource);

            // inspect data
            var trainData = data.AsDynamic;
            var columnNames = trainData.Schema.GetColumnNames().ToArray();
            var trainDataAsEnumerable = trainData.AsEnumerable<SalesPipelineData>(env, false).Take(10).ToArray();

            ConsoleWriteHeader("Evaluate model");
            var metrics = ctx.Evaluate(data, r => r.Label, r => r.preds);
            Console.WriteLine($"Accuracy is: {metrics.Accuracy}");
            Console.WriteLine($"AUC is: {metrics.Auc}");
        }


        protected PredictionFunction<SalesData, SalesPrediction> LoadModel(string modelLocation)
        {
            ConsoleWriteHeader("Load Model");
            Console.WriteLine($"Model file location: {modelLocation}");
            using (var file = File.OpenRead(modelLocation))
            {
                return TransformerChain
                    .LoadFrom(env, file)
                    .MakePredictionFunction<SalesData, SalesPrediction>(env);
            }
        }

        protected IEnumerable<SalesPrediction> PredictDataUsingModel(string testFileLocation, PredictionFunction<SalesData, SalesPrediction> model)
        {
            ConsoleWriteHeader("Predict data");
            var testData = SalesData.ReadFromCsv(testFileLocation);
            foreach (var item in testData)
            {
                yield return model.Predict(item);
            }
            Console.WriteLine($"Number of predictions: {testData.Count()}");
        }

        protected void EvaluateModel(IDataView salesData)
        {
            ConsoleWriteHeader("Evaluate model");

            var evaluator = new BinaryClassifierEvaluator(env, new BinaryClassifierEvaluator.Arguments());
            var metrics = evaluator.Evaluate(env, salesData);

            Console.WriteLine("Accuracy is: " + metrics.Accuracy);
            Console.WriteLine("AUC is: " + metrics.Auc);
        }
    }

    /// <summary>
    /// This class is based on code from ML.NET
    /// </summary>
    public static class BinaryClassifierEvaluatorExtensions
    {
        public static SimpleBinaryClassificationMetrics Evaluate(this BinaryClassifierEvaluator self, IHostEnvironment env, IDataView data, string labelColumn = DefaultColumnNames.Label,
            string probabilityColumn = DefaultColumnNames.Probability)
        {
            var ci = EvaluateUtils.GetScoreColumnInfo(env, data.Schema, null, DefaultColumnNames.Score, MetadataUtils.Const.ScoreColumnKind.BinaryClassification);
            var rmd = BuildRoleMappedData(data, labelColumn, probabilityColumn, ci.Name);

            var metricsDict = self.Evaluate(rmd);
            return BuildMetrics(env, metricsDict);
        }

        private static RoleMappedData BuildRoleMappedData(IDataView data, string labelColumn, string probabilityColumn, string scoreColumn)
        {
            var map = new KeyValuePair<RoleMappedSchema.ColumnRole, string>[]
            {
                RoleMappedSchema.CreatePair(MetadataUtils.Const.ScoreValueKind.Probability, probabilityColumn),
                RoleMappedSchema.CreatePair(MetadataUtils.Const.ScoreValueKind.Score, scoreColumn)
            };
            var rmd = new RoleMappedData(data, labelColumn, DefaultColumnNames.Features, opt: true, custom: map);
            return rmd;
        }

        private static SimpleBinaryClassificationMetrics BuildMetrics(IHostEnvironment env, Dictionary<string, IDataView> metricsDict)
        {
            var overallMetrics = metricsDict[MetricKinds.OverallMetrics];
            var metricsEnumerable = overallMetrics.AsEnumerable<SimpleBinaryClassificationMetrics>(env, true, ignoreMissingColumns: true);
            return metricsEnumerable.Single();
        }

        public sealed class SimpleBinaryClassificationMetrics
        {
            public double Auc { get; private set; }
            public double Accuracy { get; private set; }
        }
    }
}
