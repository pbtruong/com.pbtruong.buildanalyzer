using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace PBTruong.BuildAnalyzer.Editor
{
    public static class AssetCategoryResolver
    {
        private static readonly Dictionary<string, AssetCategory> CategoryByExtension = new(StringComparer.OrdinalIgnoreCase)
        {
            { ".png", AssetCategory.Texture }, { ".jpg", AssetCategory.Texture }, { ".jpeg", AssetCategory.Texture },
            { ".psd", AssetCategory.Texture }, { ".tga", AssetCategory.Texture }, { ".tif", AssetCategory.Texture },
            { ".tiff", AssetCategory.Texture }, { ".exr", AssetCategory.Texture }, { ".hdr", AssetCategory.Texture },
            { ".bmp", AssetCategory.Texture }, { ".gif", AssetCategory.Texture }, { ".spriteatlas", AssetCategory.Texture },
            { ".spriteatlasv2", AssetCategory.Texture }, { ".renderTexture", AssetCategory.Texture },
            { ".fbx", AssetCategory.Mesh }, { ".obj", AssetCategory.Mesh }, { ".blend", AssetCategory.Mesh }, { ".mesh", AssetCategory.Mesh },
            { ".wav", AssetCategory.Audio }, { ".mp3", AssetCategory.Audio }, { ".ogg", AssetCategory.Audio },
            { ".aif", AssetCategory.Audio }, { ".aiff", AssetCategory.Audio }, { ".mixer", AssetCategory.Audio },
            { ".anim", AssetCategory.Animation }, { ".controller", AssetCategory.Animation },
            { ".overrideController", AssetCategory.Animation }, { ".playable", AssetCategory.Animation },
            { ".shader", AssetCategory.Shader }, { ".shadergraph", AssetCategory.Shader }, { ".compute", AssetCategory.Shader },
            { ".shadervariants", AssetCategory.Shader }, { ".hlsl", AssetCategory.Shader }, { ".cginc", AssetCategory.Shader },
            { ".mat", AssetCategory.Material },
            { ".ttf", AssetCategory.Font }, { ".otf", AssetCategory.Font }, { ".fontsettings", AssetCategory.Font },
            { ".mp4", AssetCategory.Video }, { ".webm", AssetCategory.Video }, { ".mov", AssetCategory.Video },
            { ".json", AssetCategory.ScriptData }, { ".bytes", AssetCategory.ScriptData }, { ".txt", AssetCategory.ScriptData },
            { ".csv", AssetCategory.ScriptData }, { ".xml", AssetCategory.ScriptData }, { ".cs", AssetCategory.ScriptData },
            { ".dll", AssetCategory.ScriptData },
            { ".prefab", AssetCategory.Prefab },
            { ".unity", AssetCategory.Scene }
        };

        private static readonly (string keyword, AssetCategory category)[] CategoryByTypeKeyword =
        {
            ("Texture", AssetCategory.Texture), ("Sprite", AssetCategory.Texture), ("Cubemap", AssetCategory.Texture),
            ("Mesh", AssetCategory.Mesh),
            ("Audio", AssetCategory.Audio),
            ("Anim", AssetCategory.Animation), ("Avatar", AssetCategory.Animation),
            ("Shader", AssetCategory.Shader),
            ("Material", AssetCategory.Material),
            ("Font", AssetCategory.Font),
            ("Video", AssetCategory.Video),
            ("Scene", AssetCategory.Scene),
            ("GameObject", AssetCategory.Prefab), ("Prefab", AssetCategory.Prefab), ("Transform", AssetCategory.Prefab),
            ("MonoBehaviour", AssetCategory.ScriptData), ("MonoScript", AssetCategory.ScriptData),
            ("ScriptableObject", AssetCategory.ScriptData), ("TextAsset", AssetCategory.ScriptData)
        };

        public static AssetCategory Resolve(string assetPath, string typeName)
        {
            var extension = Path.GetExtension(assetPath ?? string.Empty);
            if (!string.IsNullOrEmpty(extension) && CategoryByExtension.TryGetValue(extension, out var byExtension))
                return byExtension;

            var mainType = string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            var byMainType = FromTypeName(mainType?.Name);
            return byMainType != AssetCategory.Other ? byMainType : FromTypeName(typeName);
        }

        public static AssetCategory FromTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return AssetCategory.Other;

            foreach (var (keyword, category) in CategoryByTypeKeyword)
            {
                if (typeName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return category;
            }

            return AssetCategory.Other;
        }
    }
}
