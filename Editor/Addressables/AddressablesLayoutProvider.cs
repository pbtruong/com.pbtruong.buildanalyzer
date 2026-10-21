using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.AddressableAssets.Settings;
using AddressablesRuntime = UnityEngine.AddressableAssets.Addressables;

namespace PBTruong.BuildAnalyzer.Editor.Addressables
{
    internal sealed class AddressablesLayoutProvider : IBundleLayoutProvider
    {
        private const string HistoryPrefix = "buildlayout_";
        private const string HistoryTimeFormat = "yyyy.MM.dd.HH.mm.ss";
        private const string LatestFileName = "buildlayout.json";
        private const string BuiltInGroupName = "(Built-in)";

        public bool IsLayoutGenerationEnabled
        {
            get => ProjectConfigData.GenerateBuildLayout;
            set => ProjectConfigData.GenerateBuildLayout = value;
        }

        public IReadOnlyList<LayoutFileInfo> FindLayoutFiles()
        {
            var files = new List<LayoutFileInfo>();
            if (Directory.Exists(AddressablesRuntime.BuildReportPath))
            {
                files.AddRange(Directory.GetFiles(AddressablesRuntime.BuildReportPath, HistoryPrefix + "*.json")
                    .Select(path => new LayoutFileInfo(path, ParseTimestamp(path))));
            }

            var latest = Path.Combine(AddressablesRuntime.LibraryPath, LatestFileName);
            if (files.Count == 0 && File.Exists(latest))
                files.Add(new LayoutFileInfo(latest, File.GetLastWriteTime(latest)));

            return files.OrderByDescending(file => file.Timestamp).ToList();
        }

        public BundleLayoutData Load(string path)
        {
            var layout = BuildLayout.Open(path, readHeader: true, readFullFile: true);
            if (layout == null)
                return null;

            var groups = layout.Groups
                .Select(group => new BundleGroupInfo(group.Name, group.Bundles.Select(bundle => ConvertBundle(bundle, false)).ToList()))
                .ToList();
            if (layout.BuiltInBundles.Count > 0)
                groups.Add(new BundleGroupInfo(BuiltInGroupName, layout.BuiltInBundles.Select(bundle => ConvertBundle(bundle, true)).ToList()));

            return new BundleLayoutData(path, layout.BuildTarget.ToString(), layout.BuildStart, groups);
        }

        private static BundleInfo ConvertBundle(BuildLayout.Bundle bundle, bool isBuiltIn)
        {
            var assets = new List<BundleAssetInfo>();
            foreach (var file in bundle.Files)
            {
                assets.AddRange(file.Assets.Select(asset => new BundleAssetInfo(
                    asset.AssetPath, asset.AddressableName, asset.MainAssetType.ToString(), (long)(asset.SerializedSize + asset.StreamedSize), false,
                    ReferencingPaths(asset.ReferencingAssets))));
                assets.AddRange(file.OtherAssets.Select(asset => new BundleAssetInfo(
                    asset.AssetPath, string.Empty, asset.MainAssetType.ToString(), (long)(asset.SerializedSize + asset.StreamedSize), true,
                    ReferencingPaths(asset.ReferencingAssets))));
            }

            return new BundleInfo(bundle.Name, bundle.Compression, bundle.LoadPath, (long)bundle.FileSize, (long)bundle.UncompressedFileSize, assets, isBuiltIn);
        }

        private static IReadOnlyList<string> ReferencingPaths(List<BuildLayout.ExplicitAsset> referencing) =>
            referencing == null || referencing.Count == 0
                ? Array.Empty<string>()
                : referencing.Where(asset => asset != null).Select(asset => asset.AssetPath).Distinct().ToList();

        private static DateTime ParseTimestamp(string path)
        {
            var stamp = Path.GetFileNameWithoutExtension(path).Substring(HistoryPrefix.Length);
            return DateTime.TryParseExact(stamp, HistoryTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp)
                ? timestamp
                : File.GetLastWriteTime(path);
        }
    }
}
