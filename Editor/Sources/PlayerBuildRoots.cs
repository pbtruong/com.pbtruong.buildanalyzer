using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class PlayerBuildRoots : IBuildRootProvider
    {
        public IEnumerable<BuildRoot> CollectRoots()
        {
            foreach (var scene in EditorBuildSettings.scenes.Where(scene => scene.enabled))
                yield return new BuildRoot(scene.path, BuildRootKind.Scene, "Scene In Build");

            foreach (var asset in PlayerSettings.GetPreloadedAssets().Where(asset => asset != null))
                yield return new BuildRoot(AssetDatabase.GetAssetPath(asset), BuildRootKind.Preloaded, "Preloaded Asset");
        }
    }

    internal sealed class ResourcesBuildRoots : IBuildRootProvider
    {
        public IEnumerable<BuildRoot> CollectRoots() =>
            AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal)
                               && path.Contains("/Resources/")
                               && !path.Contains("/Editor/")
                               && !AssetDatabase.IsValidFolder(path))
                .Select(path => new BuildRoot(path, BuildRootKind.Resources, "Resources"));
    }
}
