using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace PBTruong.BuildAnalyzer.Editor.Addressables
{
    internal sealed class AddressablesBuildRoots : IBuildRootProvider
    {
        public IEnumerable<BuildRoot> CollectRoots()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                yield break;

            foreach (var group in settings.groups)
            {
                var schema = group != null ? group.GetSchema<BundledAssetGroupSchema>() : null;
                if (schema == null || !schema.IncludeInBuild)
                    continue;

                var entries = new List<AddressableAssetEntry>();
                foreach (var entry in group.entries)
                    entry.GatherAllAssets(entries, true, true, false);

                foreach (var path in entries.Select(entry => entry.AssetPath).Where(path => !AssetDatabase.IsValidFolder(path)).Distinct())
                    yield return new BuildRoot(path, BuildRootKind.Addressables, $"Addressables: {group.Name}");
            }
        }
    }
}
