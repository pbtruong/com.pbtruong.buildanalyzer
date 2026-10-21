using System;
using System.Collections.Generic;
using System.Linq;

namespace PBTruong.BuildAnalyzer.Editor
{
    public sealed class UsagePath
    {
        public UsagePath(BuildRoot root, IReadOnlyList<string> chain)
        {
            Root = root;
            Chain = chain;
        }

        public BuildRoot Root { get; }
        public IReadOnlyList<string> Chain { get; }
        public string ChainText => string.Join("  →  ", Chain.Select(AssetPaths.DisplayName));
    }

    public sealed class BundleEvidence
    {
        public BundleEvidence(string group, string bundle, bool isImplicit, IReadOnlyList<string> referencedBy)
        {
            Group = group;
            Bundle = bundle;
            IsImplicit = isImplicit;
            ReferencedBy = referencedBy;
        }

        public string Group { get; }
        public string Bundle { get; }
        public bool IsImplicit { get; }
        public IReadOnlyList<string> ReferencedBy { get; }
    }

    public sealed class UsageReport
    {
        public UsageReport(
            string assetPath,
            IReadOnlyList<BuildRoot> ownRoots,
            IReadOnlyList<UsagePath> paths,
            bool pathsTruncated,
            SizeEntry playerEntry,
            IReadOnlyList<string> playerScenes,
            IReadOnlyList<BundleEvidence> bundles)
        {
            AssetPath = assetPath;
            OwnRoots = ownRoots;
            Paths = paths;
            PathsTruncated = pathsTruncated;
            PlayerEntry = playerEntry;
            PlayerScenes = playerScenes;
            Bundles = bundles;
        }

        public string AssetPath { get; }
        public IReadOnlyList<BuildRoot> OwnRoots { get; }
        public IReadOnlyList<UsagePath> Paths { get; }
        public bool PathsTruncated { get; }
        public SizeEntry PlayerEntry { get; }
        public IReadOnlyList<string> PlayerScenes { get; }
        public IReadOnlyList<BundleEvidence> Bundles { get; }
        public bool IsReachable => Paths.Count > 0;
        public bool IsInBuildData => PlayerEntry != null || Bundles.Count > 0;
    }

    public static class UsageResolver
    {
        public const int MaxPaths = 200;

        public static UsageReport Resolve(string assetPath, DependencyGraph graph, PlayerBuildData player, BundleLayoutData layout)
        {
            var paths = FindPathsToRoots(assetPath, graph, MaxPaths + 1);
            var truncated = paths.Count > MaxPaths;
            if (truncated)
                paths.RemoveAt(paths.Count - 1);

            var playerEntry = player?.Assets.FirstOrDefault(entry => entry.AssetPath == assetPath);
            var scenes = player != null && player.ScenesUsingAsset.TryGetValue(assetPath, out var scenePaths)
                ? (IReadOnlyList<string>)scenePaths
                : Array.Empty<string>();

            return new UsageReport(assetPath, graph.RootsAt(assetPath), paths, truncated, playerEntry, scenes, FindBundles(assetPath, layout));
        }

        public static List<UsagePath> FindPathsToRoots(string assetPath, DependencyGraph graph, int limit)
        {
            var result = new List<UsagePath>();
            var next = new Dictionary<string, string> { [assetPath] = null };
            var queue = new Queue<string>();
            queue.Enqueue(assetPath);

            while (queue.Count > 0 && result.Count < limit)
            {
                var path = queue.Dequeue();
                foreach (var root in graph.RootsAt(path))
                {
                    result.Add(new UsagePath(root, ChainFrom(path, next)));
                    if (result.Count >= limit)
                        break;
                }

                foreach (var referencer in graph.ReferencersOf(path))
                {
                    if (next.ContainsKey(referencer))
                        continue;
                    next.Add(referencer, path);
                    queue.Enqueue(referencer);
                }
            }

            return result;
        }

        private static IReadOnlyList<string> ChainFrom(string start, IReadOnlyDictionary<string, string> next)
        {
            var chain = new List<string>();
            for (var current = start; current != null; current = next[current])
                chain.Add(current);
            return chain;
        }

        private static IReadOnlyList<BundleEvidence> FindBundles(string assetPath, BundleLayoutData layout)
        {
            if (layout == null)
                return Array.Empty<BundleEvidence>();

            var result = new List<BundleEvidence>();
            foreach (var group in layout.Groups)
            {
                foreach (var bundle in group.Bundles)
                {
                    foreach (var asset in bundle.Assets.Where(asset => asset.AssetPath == assetPath))
                        result.Add(new BundleEvidence(group.Name, bundle.Name, asset.IsImplicit, asset.ReferencedBy));
                }
            }

            return result;
        }
    }
}
