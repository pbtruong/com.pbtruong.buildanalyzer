using System;
using System.Collections.Generic;
using UnityEditor;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class LargeTextureRule : ISizeInsightRule
    {
        private const int LargeDimension = 2048;
        private static readonly string[] CompressedFormatKeywords = { "DXT", "BC", "ETC", "EAC", "ASTC", "PVRTC", "Crunched" };

        public string Name => "Large Texture";

        public IEnumerable<SizeInsight> Evaluate(InsightContext context)
        {
            foreach (var entry in context.Assets)
            {
                if (entry.Category != AssetCategory.Texture || entry.Size < context.Settings.LargeTextureThreshold)
                    continue;
                if (AssetImporter.GetAtPath(entry.AssetPath) is not TextureImporter importer)
                    continue;

                yield return Inspect(entry, importer, context.PlatformName);
            }
        }

        private SizeInsight Inspect(SizeEntry entry, TextureImporter importer, string platform)
        {
            var platformSettings = importer.GetPlatformTextureSettings(platform);
            var maxSize = platformSettings.overridden ? platformSettings.maxTextureSize : importer.maxTextureSize;
            var format = platformSettings.overridden ? platformSettings.format : importer.GetAutomaticFormat(platform);
            importer.GetSourceTextureWidthAndHeight(out var width, out var height);

            var severity = InsightSeverity.Info;
            var problems = new List<string> { $"{width}x{height}, max size {maxSize}, {format}" };
            var fixes = new List<string>();

            if (!IsCompressed(format))
            {
                severity = InsightSeverity.Critical;
                problems.Add("uncompressed");
                fixes.Add($"Use an ASTC/ETC2 override for {platform}");
            }

            if (importer.mipmapEnabled && importer.textureType == TextureImporterType.Sprite)
            {
                severity = Max(severity, InsightSeverity.Warning);
                problems.Add("mipmaps on a sprite (+33%)");
                fixes.Add("Disable Generate Mip Maps");
            }

            if (Math.Min(Math.Max(width, height), maxSize) >= LargeDimension)
            {
                problems.Add($"imported at {LargeDimension}px or more");
                fixes.Add("Lower Max Size if it is never displayed that large");
            }

            var suggestion = fixes.Count > 0 ? string.Join(". ", fixes) : "Check whether resolution or format can be reduced";
            return new SizeInsight(Name, severity, entry.AssetPath, string.Join("; ", problems), suggestion, entry.Size);
        }

        internal static bool IsCompressed(TextureImporterFormat format)
        {
            var name = format.ToString();
            foreach (var keyword in CompressedFormatKeywords)
            {
                if (name.IndexOf(keyword, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static InsightSeverity Max(InsightSeverity a, InsightSeverity b) => a > b ? a : b;
    }
}
