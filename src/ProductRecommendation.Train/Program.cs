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
            // Running inside Visual Studio, $SolutionDir/assets is automatically passed as argument
            // If you execute from the console, pass as argument the location of the assets folder
            // Otherwise, it will search for assets in the executable's folder
            var assetsPath = args.Length > 0 ? args[0] : ModelHelpers.GetAssetsPath();

            var salesCsv = Path.Combine(assetsPath, "inputs", "orderItems.csv");
            var modelZip = Path.Combine(assetsPath, "outputs", "productRecommendation.zip");

            try
            {
                var modelBuilder = new ModelBuilder(salesCsv, modelZip);
                modelBuilder.BuildAndTrain();

                modelBuilder.Test();
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine(ex.Message);
            }

            Console.ReadKey();
        }
    }
}
