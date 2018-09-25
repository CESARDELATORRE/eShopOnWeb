using Microsoft.ML;
using Microsoft.ML.Runtime.Data;
using ProductRecommendation.Train.ProductData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.eShopWeb.Infrastructure.Services
{
    public class DataSetReaderService : IDataSetReaderService
    {
        private readonly string dataSetLocation;
        private readonly ConsoleEnvironment env;

        public DataSetReaderService()
        {
            FileInfo currentAssemblyLocation = new FileInfo(typeof(ProductRecommendationService).Assembly.Location);
            dataSetLocation = Path.Combine(currentAssemblyLocation.Directory.FullName,
                                           "Setup",
                                           "historic-dataset",
                                           "orderItemsPre.csv");
            env = new ConsoleEnvironment();
        }

        public IEnumerable<string> GetProductsBoughtByUser(string user, string[] products, int numberOfProductsInPage)
        {           

            //Get the products bought by the user in the past
            var sales = SalesData.ReadFromCsv(this.dataSetLocation).ToArray();


            IEnumerable<string> productsBoughtByUser = sales
                                                        .Where(p => p.CustomerId == user)
                                                        .Select(p => p.ProductId)
                                                        .Take(numberOfProductsInPage);
            return productsBoughtByUser;

        }


    }

    public interface IDataSetReaderService
    {
        IEnumerable<string> GetProductsBoughtByUser(string user, string[] products, int numberOfProductsInPage);
    }

}
