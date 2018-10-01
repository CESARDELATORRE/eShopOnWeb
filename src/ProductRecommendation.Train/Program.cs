using CustomerSegmentation.Model;
using System;
using System.IO;
using static CustomerSegmentation.Model.ConsoleHelpers;

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
                modelBuilder.BuildAndTrainStaticApi();
            }
            catch (Exception ex)
            {
                ConsoleWriteException(ex.Message);
            }

            ConsolePressAnyKey();
        }
    }
}
