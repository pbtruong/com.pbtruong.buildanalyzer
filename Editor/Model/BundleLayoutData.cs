using System;
using System.Collections.Generic;
using System.Linq;

namespace PBTruong.BuildAnalyzer.Editor
{
    public sealed class BundleAssetInfo
    {
        public BundleAssetInfo(string assetPath, string address, string typeName, long size, bool isImplicit, IReadOnlyList<string> referencedBy = null)
        {
            AssetPath = assetPath;
            Address = address;
            TypeName = typeName;
            Category = AssetCategoryResolver.Resolve(assetPath, typeName);
            Size = size;
            IsImplicit = isImplicit;
            ReferencedBy = referencedBy ?? Array.Empty<string>();
        }

        public string AssetPath { get; }
        public string Address { get; }
        public string TypeName { get; }
        public AssetCategory Category { get; }
        public long Size { get; }
        public bool IsImplicit { get; }
        public IReadOnlyList<string> ReferencedBy { get; }
        public string Name => AssetPaths.DisplayName(AssetPath);
    }

    public sealed class BundleInfo
    {
        public BundleInfo(string name, string compression, string loadPath, long compressedSize, long uncompressedSize, IReadOnlyList<BundleAssetInfo> assets, bool isBuiltIn)
        {
            Name = name;
            Compression = compression;
            LoadPath = loadPath;
            CompressedSize = compressedSize;
            UncompressedSize = uncompressedSize;
            Assets = assets;
            IsBuiltIn = isBuiltIn;
        }

        public string Name { get; }
        public string Compression { get; }
        public string LoadPath { get; }
        public long CompressedSize { get; }
        public long UncompressedSize { get; }
        public IReadOnlyList<BundleAssetInfo> Assets { get; }
        public bool IsBuiltIn { get; }
    }

    public sealed class BundleGroupInfo
    {
        public BundleGroupInfo(string name, IReadOnlyList<BundleInfo> bundles)
        {
            Name = name;
            Bundles = bundles;
            CompressedSize = bundles.Sum(bundle => bundle.CompressedSize);
            UncompressedSize = bundles.Sum(bundle => bundle.UncompressedSize);
            AssetCount = bundles.Sum(bundle => bundle.Assets.Count);
        }

        public string Name { get; }
        public IReadOnlyList<BundleInfo> Bundles { get; }
        public long CompressedSize { get; }
        public long UncompressedSize { get; }
        public int AssetCount { get; }
    }

    public sealed class DuplicateAssetInfo
    {
        public DuplicateAssetInfo(string assetPath, AssetCategory category, long sizePerCopy, IReadOnlyList<string> bundles)
        {
            AssetPath = assetPath;
            Category = category;
            SizePerCopy = sizePerCopy;
            Bundles = bundles;
        }

        public string AssetPath { get; }
        public AssetCategory Category { get; }
        public long SizePerCopy { get; }
        public IReadOnlyList<string> Bundles { get; }
        public long WastedSize => SizePerCopy * (Bundles.Count - 1);
        public string Name => AssetPaths.DisplayName(AssetPath);
    }

    public sealed class BundleLayoutData
    {
        public BundleLayoutData(string filePath, string platform, DateTime buildStart, IReadOnlyList<BundleGroupInfo> groups)
        {
            FilePath = filePath;
            Platform = platform;
            BuildStart = buildStart;
            Groups = groups;
            Bundles = groups.SelectMany(group => group.Bundles).ToList();
            TotalCompressedSize = groups.Sum(group => group.CompressedSize);
            TotalUncompressedSize = groups.Sum(group => group.UncompressedSize);
            Assets = AggregateAssets(Bundles);
            Duplicates = FindDuplicates(Bundles);
        }

        public string FilePath { get; }
        public string Platform { get; }
        public DateTime BuildStart { get; }
        public IReadOnlyList<BundleGroupInfo> Groups { get; }
        public IReadOnlyList<BundleInfo> Bundles { get; }
        public IReadOnlyList<SizeEntry> Assets { get; }
        public IReadOnlyList<DuplicateAssetInfo> Duplicates { get; }
        public long TotalCompressedSize { get; }
        public long TotalUncompressedSize { get; }
        public long DuplicatedWaste => Duplicates.Sum(duplicate => duplicate.WastedSize);

        private static IReadOnlyList<SizeEntry> AggregateAssets(IEnumerable<BundleInfo> bundles)
        {
            var accumulators = new Dictionary<string, AssetAccumulator>();
            foreach (var bundle in bundles)
            {
                foreach (var asset in bundle.Assets)
                {
                    if (!accumulators.TryGetValue(asset.AssetPath, out var accumulator))
                    {
                        accumulator = new AssetAccumulator();
                        accumulators.Add(asset.AssetPath, accumulator);
                    }

                    accumulator.Add(asset.TypeName, asset.Size, bundle.Name, asset.IsImplicit);
                }
            }

            return accumulators.Select(pair => pair.Value.ToEntry(pair.Key, SizeSource.Addressables)).ToList();
        }

        private static IReadOnlyList<DuplicateAssetInfo> FindDuplicates(IEnumerable<BundleInfo> bundles) =>
            bundles
                .SelectMany(bundle => bundle.Assets.Where(asset => asset.IsImplicit).Select(asset => (bundle: bundle.Name, asset)))
                .GroupBy(occurrence => occurrence.asset.AssetPath)
                .Select(group => (path: group.Key, bundles: group.Select(o => o.bundle).Distinct().ToList(), size: group.Max(o => o.asset.Size), category: group.First().asset.Category))
                .Where(candidate => candidate.bundles.Count > 1)
                .Select(candidate => new DuplicateAssetInfo(candidate.path, candidate.category, candidate.size, candidate.bundles))
                .OrderByDescending(duplicate => duplicate.WastedSize)
                .ToList();
    }
}
