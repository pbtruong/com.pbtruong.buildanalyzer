using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace PBTruong.BuildAnalyzer.Editor
{
    public static class TextureCompressionApplier
    {
        private const string ProgressTitle = "Build Analyzer";

        public static int Apply(IReadOnlyList<string> texturePaths, IReadOnlyList<TextureCompressionPlan> plans)
        {
            var active = plans.Where(plan => plan.HasChanges).ToList();
            if (active.Count == 0)
                return 0;

            var changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                for (var i = 0; i < texturePaths.Count; i++)
                {
                    var path = texturePaths[i];
                    if (EditorUtility.DisplayCancelableProgressBar(ProgressTitle, $"Updating {Path.GetFileName(path)}", i / (float)texturePaths.Count))
                        break;
                    if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                        continue;

                    var dirty = false;
                    foreach (var plan in active)
                        dirty |= plan.Apply(importer);
                    if (!dirty)
                        continue;

                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            return changed;
        }
    }
}
