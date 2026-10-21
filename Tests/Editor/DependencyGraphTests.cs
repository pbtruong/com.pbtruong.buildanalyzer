using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace PBTruong.BuildAnalyzer.Editor.Tests
{
    public sealed class DependencyGraphTests
    {
        private const string Scene = "Assets/Scenes/Home.unity";
        private const string Prefab = "Assets/Prefabs/Fish.prefab";
        private const string Material = "Assets/Materials/Fish.mat";
        private const string Texture = "Assets/Textures/Fish.png";
        private const string Script = "Assets/Scripts/Fish.cs";
        private const string Orphan = "Assets/Textures/Unused.png";
        private const string AddressablePrefab = "Assets/Prefabs/Shop.prefab";

        private static DependencyGraph CreateGraph()
        {
            var edges = new Dictionary<string, string[]>
            {
                [Scene] = new[] { Prefab, Script },
                [Prefab] = new[] { Material, Script, Prefab },
                [Material] = new[] { Texture },
                [AddressablePrefab] = new[] { Material, Scene }
            };
            var roots = new[]
            {
                new BuildRoot(Scene, BuildRootKind.Scene, "Scene In Build"),
                new BuildRoot(AddressablePrefab, BuildRootKind.Addressables, "Addressables: Shop")
            };
            return DependencyGraph.Build(roots, path => edges.TryGetValue(path, out var deps) ? deps : Array.Empty<string>());
        }

        [Test]
        public void Build_FollowsDependenciesFromRootsAndSkipsScripts()
        {
            var graph = CreateGraph();

            Assert.That(graph.Contains(Texture), Is.True);
            Assert.That(graph.Contains(Script), Is.False);
            Assert.That(graph.Contains(Orphan), Is.False);
            Assert.That(graph.DependenciesOf(Prefab), Is.EqualTo(new[] { Material }));
        }

        [Test]
        public void ReferencersOf_ReturnsDirectReferencers()
        {
            var graph = CreateGraph();

            Assert.That(graph.ReferencersOf(Material), Is.EquivalentTo(new[] { Prefab, AddressablePrefab }));
            Assert.That(graph.ReferencersOf(Texture), Is.EquivalentTo(new[] { Material }));
        }

        [Test]
        public void FindPathsToRoots_ReturnsShortestChainPerRoot()
        {
            var paths = UsageResolver.FindPathsToRoots(Texture, CreateGraph(), 10);

            Assert.That(paths.Select(path => path.Root.AssetPath), Is.EquivalentTo(new[] { Scene, AddressablePrefab }));
            Assert.That(paths.Single(path => path.Root.AssetPath == AddressablePrefab).Chain, Is.EqualTo(new[] { AddressablePrefab, Material, Texture }));
            Assert.That(paths.Single(path => path.Root.AssetPath == Scene).Chain, Is.EqualTo(new[] { Scene, Prefab, Material, Texture }));
        }

        [Test]
        public void FindPathsToRoots_RootItselfHasSingleStepChain()
        {
            var paths = UsageResolver.FindPathsToRoots(Scene, CreateGraph(), 10);

            Assert.That(paths.First().Chain, Is.EqualTo(new[] { Scene }));
        }

        [Test]
        public void FindPathsToRoots_RespectsLimit()
        {
            Assert.That(UsageResolver.FindPathsToRoots(Texture, CreateGraph(), 1), Has.Count.EqualTo(1));
        }

        [Test]
        public void FindPathsToRoots_UnreachableAssetHasNoPaths()
        {
            Assert.That(UsageResolver.FindPathsToRoots(Orphan, CreateGraph(), 10), Is.Empty);
        }
    }
}
