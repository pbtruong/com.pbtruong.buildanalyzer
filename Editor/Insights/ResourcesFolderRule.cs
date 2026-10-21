using System;
using System.Collections.Generic;
using System.Linq;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class ResourcesFolderRule : ISizeInsightRule
    {
        public string Name => "Resources Folder";

        public IEnumerable<SizeInsight> Evaluate(InsightContext context)
        {
            if (context.Player == null)
                return Array.Empty<SizeInsight>();

            return context.Player.Assets
                .Where(entry => entry.Size >= context.Settings.MinResourcesAssetSize && PlayerLocations.IsIn(entry, PlayerLocation.Resources))
                .Select(entry => new SizeInsight(
                    Name,
                    InsightSeverity.Info,
                    entry.AssetPath,
                    "Packed into resources.assets: always shipped, whether used or not",
                    "Move it to Addressables, or delete it if it is no longer loaded",
                    entry.Size));
        }
    }
}
