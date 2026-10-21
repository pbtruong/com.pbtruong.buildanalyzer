using UnityEditor;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class TextureRow
    {
        public TextureRow(string path, TextureImporter importer, string atlas)
        {
            Path = path;
            Importer = importer;
            Atlas = atlas;
            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            Width = width;
            Height = height;
            HasAlpha = importer.DoesSourceTextureHaveAlpha();
            TypeName = importer.textureType.ToString();
        }

        public string Path { get; }
        public TextureImporter Importer { get; }
        public string Atlas { get; }
        public int Width { get; }
        public int Height { get; }
        public bool HasAlpha { get; }
        public string TypeName { get; }
        public string Name => AssetPaths.DisplayName(Path);
        public string SourceSize => $"{Width}x{Height}";
        public bool IsPacked => Atlas != null;

        public bool IsChecked { get; set; }
        public long BuildSize { get; set; } = -1;
        public string CurrentText { get; set; } = string.Empty;
        public long CurrentBytes { get; set; } = TextureFormats.Unknown;
        public string NewText { get; set; } = string.Empty;
        public long NewBytes { get; set; } = TextureFormats.Unknown;
        public bool WillChange { get; set; }
        public string Flags { get; set; } = string.Empty;
    }
}
