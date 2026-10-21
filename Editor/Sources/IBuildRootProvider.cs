using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace PBTruong.BuildAnalyzer.Editor
{
    public enum BuildRootKind
    {
        Scene,
        Preloaded,
        Resources,
        Addressables
    }

    public sealed class BuildRoot
    {
        public BuildRoot(string assetPath, BuildRootKind kind, string label)
        {
            AssetPath = assetPath;
            Kind = kind;
            Label = label;
        }

        public string AssetPath { get; }
        public BuildRootKind Kind { get; }
        public string Label { get; }
    }

    public interface IBuildRootProvider
    {
        IEnumerable<BuildRoot> CollectRoots();
    }

    public static class BuildRootLocator
    {
        public static IReadOnlyList<BuildRoot> CollectAll() =>
            TypeCache.GetTypesDerivedFrom<IBuildRootProvider>()
                .Where(type => !type.IsAbstract && !type.IsInterface && type.GetConstructor(Type.EmptyTypes) != null)
                .Select(type => (IBuildRootProvider)Activator.CreateInstance(type))
                .SelectMany(provider => provider.CollectRoots())
                .Where(root => !string.IsNullOrEmpty(root.AssetPath))
                .ToList();
    }
}
