using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace PBTruong.BuildAnalyzer.Editor
{
    public static class SpriteAtlasMembership
    {
        private const string PackablesKey = "packables:";
        private static readonly Regex GuidPattern = new(@"guid:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);
        private static readonly string[] AtlasExtensions = { ".spriteatlas", ".spriteatlasv2" };

        public static IReadOnlyDictionary<string, string> Build()
        {
            var atlasByTexture = new Dictionary<string, string>();
            var atlases = AssetDatabase.GetAllAssetPaths()
                .Where(path => AtlasExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase));

            foreach (var atlasPath in atlases)
            {
                var atlasName = Path.GetFileNameWithoutExtension(atlasPath);
                foreach (var texture in PackedTextures(atlasPath))
                    atlasByTexture.TryAdd(texture, atlasName);
            }

            return atlasByTexture;
        }

        public static IEnumerable<string> ParsePackableGuids(IEnumerable<string> yamlLines)
        {
            var inPackables = false;
            foreach (var line in yamlLines)
            {
                var trimmed = line.Trim();
                if (trimmed == PackablesKey)
                {
                    inPackables = true;
                    continue;
                }

                if (!inPackables)
                    continue;
                if (!trimmed.StartsWith("-", StringComparison.Ordinal))
                {
                    inPackables = false;
                    continue;
                }

                var match = GuidPattern.Match(trimmed);
                if (match.Success)
                    yield return match.Groups[1].Value.ToLowerInvariant();
            }
        }

        private static IEnumerable<string> PackedTextures(string atlasPath)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(atlasPath);
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"[Build Analyzer] Cannot read {atlasPath}: {exception.Message}");
                yield break;
            }

            foreach (var guid in ParsePackableGuids(lines))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    continue;

                if (!AssetDatabase.IsValidFolder(path))
                {
                    yield return path;
                    continue;
                }

                foreach (var textureGuid in AssetDatabase.FindAssets("t:Texture2D", new[] { path }))
                    yield return AssetDatabase.GUIDToAssetPath(textureGuid);
            }
        }
    }
}
