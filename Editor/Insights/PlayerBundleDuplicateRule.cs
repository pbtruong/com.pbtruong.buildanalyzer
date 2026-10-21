using System.Collections.Generic;
using System.Linq;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class PlayerBundleDuplicateRule : ISizeInsightRule
    {
        public string Name => "In Player And Bundles";

        public IEnumerable<SizeInsight> Evaluate(InsightContext context) =>
            PlayerBundleDuplicates.Find(context.Player, context.Layout)
                .Where(duplicate => duplicate.WastedSize >= context.Settings.MinDuplicateSize)
                .Select(duplicate => new SizeInsight(
                    Name,
                    InsightSeverity.Warning,
                    duplicate.AssetPath,
                    $"Shipped in the player ({duplicate.Player.Container}) and in {duplicate.Bundles.Containers.Length} bundle(s)",
                    "Stop referencing it from build scenes / Resources / project settings and load it only through Addressables " +
                    "(Addressables Analyze > Check Scene/Resources to Addressable Duplicate Dependencies)",
                    duplicate.WastedSize));
    }
}
