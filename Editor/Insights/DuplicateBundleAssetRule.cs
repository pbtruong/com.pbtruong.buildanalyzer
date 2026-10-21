using System;
using System.Collections.Generic;
using System.Linq;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class DuplicateBundleAssetRule : ISizeInsightRule
    {
        private const int ListedBundles = 4;

        public string Name => "Duplicated In Bundles";

        public IEnumerable<SizeInsight> Evaluate(InsightContext context)
        {
            if (context.Layout == null)
                return Array.Empty<SizeInsight>();

            return context.Layout.Duplicates
                .Where(duplicate => duplicate.WastedSize >= context.Settings.MinDuplicateSize)
                .Select(duplicate => new SizeInsight(
                    Name,
                    InsightSeverity.Warning,
                    duplicate.AssetPath,
                    $"Copied into {duplicate.Bundles.Count} bundles: {Describe(duplicate.Bundles)}",
                    "Make it Addressable in a shared group so bundles reference one copy (Addressables Analyze > Check Duplicate Bundle Dependencies)",
                    duplicate.WastedSize));
        }

        private static string Describe(IReadOnlyList<string> bundles)
        {
            var listed = string.Join(", ", bundles.Take(ListedBundles));
            return bundles.Count > ListedBundles ? $"{listed} +{bundles.Count - ListedBundles} more" : listed;
        }
    }
}
