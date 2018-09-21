using Microsoft.ML.Runtime;
using Microsoft.ML.Runtime.Data;
using Microsoft.ML.Runtime.Model;
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

        public static void SaveTo(this ICanSaveModel model, IHostEnvironment env, Stream outputStream)
        {
            using (var ch = env.Start("Saving pipeline"))
            {
                using (var rep = RepositoryWriter.CreateNew(outputStream, ch))
                {
                    ch.Trace("Saving transformer chain");
                    ModelSaveContext.SaveModel(rep, model, TransformerChain.LoaderSignature);
                    rep.Commit();
                }
            }
        }

        public static IEnumerable<string> GetColumnNames (this ISchema schema) {
            for (int i = 0; i < schema.ColumnCount; i++)
            {
                if (!schema.IsHidden(i))
                    yield return schema.GetColumnName(i);
            }
        }
    }
}
