using Microsoft.ML;
using Microsoft.ML.Core.Data;
using Microsoft.ML.Runtime.Api;
using Microsoft.ML.Runtime.Data;
using ProductRecommendation.Train.ProductData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.eShopWeb.Infrastructure.Services
{
    public class ProductRecommendationService : IProductRecommendationService
    {
        private readonly string modelLocation;
        private readonly LocalEnvironment env;

        public ProductRecommendationService()
        {
            FileInfo currentAssemblyLocation = new FileInfo(typeof(ProductRecommendationService).Assembly.Location);
            modelLocation = Path.Combine(currentAssemblyLocation.Directory.FullName,"Setup","model", "productRecommendation.zip");
            env = new LocalEnvironment();
        }

        public IEnumerable<string> GetRecommendationsForUser(string user, string[] products, int recommendationsInPage)
        {
            var model = LoadModel(modelLocation);

            // Create all possible SalesData objects between (unique) CustomerId x ProductId (many)
            var crossUserProducts = (from product in products
                                    select new SalesData { CustomerId = user, ProductId = product }).ToArray();

            // Execute the recommendation model with previous generated data
            var inputDataView = ComponentCreation.CreateDataView(env, crossUserProducts);
            var predictions = model
                .Transform(inputDataView)
                .AsEnumerable<SalesPrediction>(env, false)
                .ToArray();

            //Count how many recommended products the user gets (with more or less score..)
            var numberOfRecommendedProducts = predictions.Where(x => x.Recommendation).Count();

            //Count how many recommended products the user gets (with more than 0.7 score..)
            var RecommendedProductsOverThreshold = (from p in predictions
                                                    orderby p.Score descending
                                                    //where p.Recommendation && p.Score > 0.7
                                                    select new SalesPrediction { ProductId = p.ProductId, Score = p.Score, Recommendation = p.Recommendation });

            var numberOfRecommendedProductsOverThreshold = RecommendedProductsOverThreshold.Count();

            // Return (recommendationsInPage) product Ids ordered by Score
            return predictions
                .Where(p => p.Recommendation)
                .OrderByDescending(p => p.Score)
                .Select(p => p.ProductId)
                .Take(recommendationsInPage);
        }

        private ITransformer LoadModel(string modelLocation)
        {
            using (var file = File.OpenRead(modelLocation))
            {
                return TransformerChain
                    .LoadFrom(env, file);
            }
        }
    }

    public interface IProductRecommendationService
    {
        IEnumerable<string> GetRecommendationsForUser(string user, string[] products, int recommendationsCount);
    }
}
