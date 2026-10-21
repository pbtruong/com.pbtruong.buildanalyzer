using System.Collections.Generic;
using UnityEditor;

namespace PBTruong.BuildAnalyzer.Editor
{
    public enum OverrideChange
    {
        Keep,
        Override,
        Remove
    }

    public sealed class TextureCompressionPlan
    {
        public TextureCompressionPlan(string platform) => Platform = platform;

        public string Platform { get; }
        public bool IsDefault => Platform == BuildPlatforms.DefaultName;

        public TextureImporterCompression? Compression { get; set; }
        public bool? Crunched { get; set; }
        public OverrideChange Override { get; set; }
        public TextureImporterFormat? Format { get; set; }
        public int? MaxSize { get; set; }
        public int? Quality { get; set; }
        public TextureResizeAlgorithm? Resize { get; set; }

        public bool HasChanges =>
            MaxSize.HasValue || Quality.HasValue || Resize.HasValue
            || (IsDefault ? Compression.HasValue || Crunched.HasValue : Override != OverrideChange.Keep || Format.HasValue);

        public TextureImporterPlatformSettings Current(TextureImporter importer) =>
            IsDefault ? importer.GetDefaultPlatformTextureSettings() : importer.GetPlatformTextureSettings(Platform);

        public TextureImporterPlatformSettings Preview(TextureImporter importer)
        {
            var settings = Current(importer);
            ApplyTo(settings);
            return settings;
        }

        public bool Apply(TextureImporter importer)
        {
            var before = Current(importer);
            var after = Preview(importer);
            if (SameSettings(before, after))
                return false;

            importer.SetPlatformTextureSettings(after);
            return true;
        }

        public IEnumerable<string> Describe()
        {
            if (IsDefault)
            {
                if (Compression.HasValue)
                    yield return $"Compression {Compression.Value}";
                if (Crunched.HasValue)
                    yield return Crunched.Value ? "Crunch on" : "Crunch off";
            }
            else
            {
                if (Override == OverrideChange.Remove)
                {
                    yield return "Remove override";
                    yield break;
                }

                if (Format.HasValue)
                    yield return TextureFormats.Label(Format.Value);
                else if (Override == OverrideChange.Override)
                    yield return "Override";
            }

            if (MaxSize.HasValue)
                yield return $"Max {MaxSize.Value}";
            if (Quality.HasValue)
                yield return $"Quality {Quality.Value}";
            if (Resize.HasValue)
                yield return $"Resize {Resize.Value}";
        }

        internal void ApplyTo(TextureImporterPlatformSettings settings)
        {
            if (IsDefault)
            {
                if (Compression.HasValue)
                    settings.textureCompression = Compression.Value;
                if (Crunched.HasValue)
                    settings.crunchedCompression = Crunched.Value;
            }
            else
            {
                if (Override == OverrideChange.Remove)
                {
                    settings.overridden = false;
                    return;
                }

                if (HasChanges)
                    settings.overridden = true;
                if (Format.HasValue)
                    settings.format = Format.Value;
            }

            if (MaxSize.HasValue)
                settings.maxTextureSize = MaxSize.Value;
            if (Quality.HasValue)
                settings.compressionQuality = Quality.Value;
            if (Resize.HasValue)
                settings.resizeAlgorithm = Resize.Value;
        }

        internal static bool SameSettings(TextureImporterPlatformSettings a, TextureImporterPlatformSettings b) =>
            a.overridden == b.overridden
            && a.format == b.format
            && a.maxTextureSize == b.maxTextureSize
            && a.compressionQuality == b.compressionQuality
            && a.resizeAlgorithm == b.resizeAlgorithm
            && a.textureCompression == b.textureCompression
            && a.crunchedCompression == b.crunchedCompression;
    }
}
