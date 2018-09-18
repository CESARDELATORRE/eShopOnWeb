using Microsoft.ML.Runtime.Data;
using ProductRecommendation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CustomerSegmentation.Model
{
    public static class ModelHelpers
    {
        static FileInfo currentAssemblyLocation = new FileInfo(typeof(Program).Assembly.Location);
        static private readonly string _dataRoot = Path.Combine(currentAssemblyLocation.Directory.FullName, "assets");

        public static string GetAssetsPath(params string[] paths)
        {
            if (paths == null || paths.Length == 0)
                return null;

            return Path.GetFullPath(Path.Combine(paths.Prepend(_dataRoot).ToArray()));
        }

        public static string DeleteAssets(params string[] paths)
        {
            var location = GetAssetsPath(paths);

            if (!string.IsNullOrWhiteSpace(location) && File.Exists(location))
                File.Delete(location);
            return location;
        }

        public static void ConsoleWriteHeader(params string[] lines)
        {
            var defaultColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(" ");
            foreach (var line in lines)
            {
                Console.WriteLine(line);
            }
            var maxLength = lines.Select(x => x.Length).Max();
            Console.WriteLine(new String('#', maxLength));
            Console.ForegroundColor = defaultColor;
        }
    }
}
