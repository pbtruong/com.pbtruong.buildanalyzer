using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PBTruong.BuildAnalyzer.Editor
{
    public sealed class DependencyGraph
    {
        public const string CachePath = "Library/PBTruongBuildAnalyzer/DependencyGraph.json";
        private const int CacheVersion = 1;
        private const int ProgressInterval = 200;
        private static readonly string[] IgnoredExtensions = { ".cs", ".dll", ".asmdef", ".asmref", ".rsp" };

        [Serializable]
        private sealed class CacheFile
        {
            public int version;
            public List<CachedNode> nodes = new();
        }

        [Serializable]
        private sealed class CachedNode
        {
            public string path;
            public string hash;
            public string[] dependencies;
        }

        private static readonly string[] NoPaths = Array.Empty<string>();
        private static readonly BuildRoot[] NoRoots = Array.Empty<BuildRoot>();

        private readonly Dictionary<string, string[]> _dependencies;
        private readonly Dictionary<string, string> _hashes;
        private readonly Dictionary<string, List<string>> _referencers = new();
        private readonly Dictionary<string, List<BuildRoot>> _roots = new();

        private DependencyGraph(Dictionary<string, string[]> dependencies, Dictionary<string, string> hashes, IEnumerable<BuildRoot> roots, int reused)
        {
            _dependencies = dependencies;
            _hashes = hashes;
            ReusedCount = reused;
            BuiltAt = DateTime.Now;

            foreach (var (path, deps) in dependencies)
            {
                foreach (var dependency in deps)
                {
                    if (!_referencers.TryGetValue(dependency, out var list))
                        _referencers.Add(dependency, list = new List<string>());
                    list.Add(path);
                }
            }

            foreach (var root in roots)
            {
                if (!_roots.TryGetValue(root.AssetPath, out var list))
                    _roots.Add(root.AssetPath, list = new List<BuildRoot>());
                list.Add(root);
            }
        }

        public static DependencyGraph Current { get; private set; }
        public static bool IsStale { get; internal set; }

        public DateTime BuiltAt { get; }
        public int NodeCount => _dependencies.Count;
        public int ReusedCount { get; }
        public int RootCount => _roots.Count;

        public bool Contains(string path) => _dependencies.ContainsKey(path);
        public bool IsRoot(string path) => _roots.ContainsKey(path);
        public IReadOnlyList<BuildRoot> RootsAt(string path) => _roots.TryGetValue(path, out var list) ? list : NoRoots;
        public IReadOnlyList<string> DependenciesOf(string path) => _dependencies.TryGetValue(path, out var deps) ? deps : NoPaths;
        public IReadOnlyList<string> ReferencersOf(string path) => _referencers.TryGetValue(path, out var list) ? list : NoPaths;

        public static bool IsTracked(string path) =>
            !string.IsNullOrEmpty(path)
            && AssetPaths.IsProjectAsset(path)
            && !IgnoredExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

        public static DependencyGraph Rebuild(Func<int, int, bool> isCancelled = null)
        {
            var graph = Build(BuildRootLocator.CollectAll(), LoadCache(), isCancelled);
            if (graph == null)
                return Current;

            SaveCache(graph);
            Current = graph;
            IsStale = false;
            return graph;
        }

        internal static DependencyGraph Build(IReadOnlyList<BuildRoot> roots, Func<string, IEnumerable<string>> directDependencies) =>
            Build(roots, new Dictionary<string, CachedNode>(), _ => string.Empty, directDependencies, null);

        private static DependencyGraph Build(IReadOnlyList<BuildRoot> roots, Dictionary<string, CachedNode> cache, Func<int, int, bool> isCancelled) =>
            Build(roots, cache, path => AssetDatabase.GetAssetDependencyHash(path).ToString(), path => AssetDatabase.GetDependencies(path, false), isCancelled);

        private static DependencyGraph Build(
            IReadOnlyList<BuildRoot> roots,
            Dictionary<string, CachedNode> cache,
            Func<string, string> hashOf,
            Func<string, IEnumerable<string>> directDependencies,
            Func<int, int, bool> isCancelled)
        {
            var dependencies = new Dictionary<string, string[]>();
            var hashes = new Dictionary<string, string>();
            var queue = new Queue<string>(roots.Select(root => root.AssetPath).Distinct());
            var reused = 0;
            while (queue.Count > 0)
            {
                var path = queue.Dequeue();
                if (dependencies.ContainsKey(path) || !IsTracked(path))
                    continue;

                var hash = hashOf(path);
                string[] deps;
                if (cache.TryGetValue(path, out var cached) && cached.hash == hash && cached.dependencies != null)
                {
                    deps = cached.dependencies;
                    reused++;
                }
                else
                {
                    deps = directDependencies(path).Where(dependency => dependency != path && IsTracked(dependency)).Distinct().ToArray();
                }

                dependencies.Add(path, deps);
                hashes.Add(path, hash);
                foreach (var dependency in deps)
                    queue.Enqueue(dependency);

                if (dependencies.Count % ProgressInterval == 0 && isCancelled != null && isCancelled(dependencies.Count, queue.Count))
                    return null;
            }

            return new DependencyGraph(dependencies, hashes, roots, reused);
        }

        private static Dictionary<string, CachedNode> LoadCache()
        {
            try
            {
                if (!File.Exists(CachePath))
                    return new Dictionary<string, CachedNode>();

                var file = JsonUtility.FromJson<CacheFile>(File.ReadAllText(CachePath));
                if (file == null || file.version != CacheVersion)
                    return new Dictionary<string, CachedNode>();

                return file.nodes.Where(node => !string.IsNullOrEmpty(node.path)).GroupBy(node => node.path).ToDictionary(group => group.Key, group => group.First());
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Build Analyzer] Ignoring unreadable dependency cache: {exception.Message}");
                return new Dictionary<string, CachedNode>();
            }
        }

        private static void SaveCache(DependencyGraph graph)
        {
            var file = new CacheFile { version = CacheVersion };
            foreach (var (path, deps) in graph._dependencies)
                file.nodes.Add(new CachedNode { path = path, hash = graph._hashes[path], dependencies = deps });

            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            File.WriteAllText(CachePath, JsonUtility.ToJson(file));
        }
    }

    internal sealed class DependencyGraphInvalidator : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (DependencyGraph.Current != null)
                DependencyGraph.IsStale = true;
        }
    }
}
