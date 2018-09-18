using Microsoft.ML;
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

        public ProductRecommendationService()
        {
            FileInfo currentAssemblyLocation = new FileInfo(typeof(ProductRecommendationService).Assembly.Location);
            this.modelLocation = Path.Combine(currentAssemblyLocation.Directory.FullName,"Setup","model", "productRecommendation.zip");
        }

        public async System.Threading.Tasks.Task<IEnumerable<string>> GetRecommendationsForUserAsync(string user, string[] products, int recommendationsInPage)
        {
            var model = await PredictionModel.ReadAsync<SalesData, SalesPrediction>(modelLocation);
            var crossPredictions = from product in products                                   
                                   select new SalesData { CustomerId = user, ProductId = product };

            var predictions = model.Predict(crossPredictions).ToArray();

            //Count how many recommended products the user gets (with more or less probability..)
            var numberOfRecommendedProducts = predictions.Where(x => x.Recommendation.IsTrue == true).Select(x => x.Recommendation).Count();

            //Count how many recommended products the user gets (with more than 70% probability..)
            var RecommendedProductsWithMoreThan70Percent = (from p in predictions
                                                            orderby p.Score descending
                                                            where p.Recommendation.IsTrue == true && p.Score > 0.7
                                                            select new SalesPrediction { ProductId = p.ProductId, Score = p.Score, Recommendation = p.Recommendation });

            var numberOfRecommendedProductsWithMoreThan70Percent = RecommendedProductsWithMoreThan70Percent.Count();


            return predictions.Where(p => p.Recommendation.IsTrue)
                .OrderByDescending(p => p.Score)
                .Select(p => p.ProductId)
                .Take(recommendationsInPage);
        }
    }

    public interface IProductRecommendationService
    {
        System.Threading.Tasks.Task<IEnumerable<string>> GetRecommendationsForUserAsync(string user, string[] products, int recommendationsCount);
    }
}
