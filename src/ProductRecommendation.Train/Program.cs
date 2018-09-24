using CustomerSegmentation.Model;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ProductRecommendation
{
    public class Program
    {
        static void Main(string[] args)
        {
            var assetsPath = ModelHelpers.GetAssetsPath(@"..\..\..\assets");

            var salesOriginalCsv = Path.Combine(assetsPath, "inputs", "orderItems.csv");
            var salesPreprocessedCsv = Path.Combine(assetsPath, "inputs", "orderItemsPre.csv");
            var modelZip = Path.Combine(assetsPath, "outputs", "productRecommendation.zip");

            try
            {
                var modelBuilder = new ModelBuilder(salesPreprocessedCsv, modelZip);
                //modelBuilder.PreProcess(salesOriginalCsv);
                modelBuilder.BuildAndTrainEstimatorAPI();
                //modelBuilder.BuildAndTrainStaticApi();

                //modelBuilder.Test();
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine(ex.Message);
            }

             Console.ReadKey();
        }
    }
}
