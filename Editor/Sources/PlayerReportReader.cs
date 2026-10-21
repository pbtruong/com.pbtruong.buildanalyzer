using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Build.Reporting;
using UnityEditorInternal;

namespace PBTruong.BuildAnalyzer.Editor
{
    public static class PlayerReportReader
    {
        public const string LatestLabel = "Latest build";
        private const string UnknownAssetPath = "(unknown source)";

        public static PlayerBuildData LoadLatest()
        {
            var report = BuildReport.GetLatestReport();
            return report == null ? null : Convert(report, LatestLabel);
        }

        public static PlayerBuildData LoadFromFile(string path)
        {
            var report = InternalEditorUtility.LoadSerializedFileAndForget(path).OfType<BuildReport>().FirstOrDefault();
            return report == null ? null : Convert(report, Path.GetFileNameWithoutExtension(path));
        }

        private static PlayerBuildData Convert(BuildReport report, string label)
        {
            var summary = report.summary;
            return new PlayerBuildData(
                label,
                summary.platform,
                summary.buildStartedAt,
                summary.totalTime,
                summary.result,
                summary.options,
                (long)summary.totalSize,
                summary.outputPath,
                File.Exists(summary.outputPath) ? new FileInfo(summary.outputPath).Length : -1,
                ReadAssets(report),
                ReadFiles(report),
                ReadModules(report),
                ReadScenesUsingAssets(report));
        }

        private static IReadOnlyList<SizeEntry> ReadAssets(BuildReport report)
        {
            var accumulators = new Dictionary<string, AssetAccumulator>();
            foreach (var packed in report.packedAssets)
            {
                foreach (var info in packed.contents)
                {
                    var path = string.IsNullOrEmpty(info.sourceAssetPath) ? UnknownAssetPath : AssetPaths.NormalizeBuiltIn(info.sourceAssetPath);
                    if (!accumulators.TryGetValue(path, out var accumulator))
                    {
                        accumulator = new AssetAccumulator();
                        accumulators.Add(path, accumulator);
                    }

                    accumulator.Add(info.type != null ? info.type.Name : "Unknown", (long)info.packedSize, packed.shortPath, false);
                }
            }

            return accumulators.Select(pair => pair.Value.ToEntry(pair.Key, SizeSource.Player)).ToList();
        }

        private static IReadOnlyList<PlayerOutputFile> ReadFiles(BuildReport report) =>
            report.GetFiles().Select(file => new PlayerOutputFile(file.path, file.role, (long)file.size)).ToList();

        private static IReadOnlyList<string> ReadModules(BuildReport report) =>
            report.strippingInfo == null
                ? Array.Empty<string>()
                : report.strippingInfo.includedModules.OrderBy(module => module).ToList();

        private static IReadOnlyDictionary<string, string[]> ReadScenesUsingAssets(BuildReport report)
        {
            if (report.scenesUsingAssets == null)
                return new Dictionary<string, string[]>();

            return report.scenesUsingAssets
                .SelectMany(usage => usage.list)
                .GroupBy(usage => usage.assetPath)
                .ToDictionary(group => group.Key, group => group.SelectMany(usage => usage.scenePaths).Distinct().ToArray());
        }
    }
}
