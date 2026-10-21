using System;
using System.Collections.Generic;
using System.Linq;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class UncompressedBundleRule : ISizeInsightRule
    {
        public string Name => "Uncompressed Bundle";

        public IEnumerable<SizeInsight> Evaluate(InsightContext context)
        {
            if (context.Layout == null)
                return Array.Empty<SizeInsight>();

            return context.Layout.Bundles
                .Where(bundle => bundle.CompressedSize >= context.Settings.MinUncompressedBundleSize
                                 && string.Equals(bundle.Compression, "Uncompressed", StringComparison.OrdinalIgnoreCase))
                .Select(bundle => new SizeInsight(
                    Name,
                    InsightSeverity.Warning,
                    bundle.Name,
                    $"Bundle is stored uncompressed ({bundle.Assets.Count} assets)",
                    "Set the group's Asset Bundle Compression to LZ4",
                    bundle.CompressedSize));
        }
    }
}
