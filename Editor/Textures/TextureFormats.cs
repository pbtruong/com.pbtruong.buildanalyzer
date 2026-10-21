using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace PBTruong.BuildAnalyzer.Editor
{
    public static class TextureFormats
    {
        public const long Unknown = -1;

        private static readonly (TextureImporterFormat format, string label, float bitsPerPixel)[] Known =
        {
            (TextureImporterFormat.Automatic, "Automatic", 0),
            (TextureImporterFormat.ASTC_4x4, "ASTC 4x4", 8f),
            (TextureImporterFormat.ASTC_5x5, "ASTC 5x5", 5.12f),
            (TextureImporterFormat.ASTC_6x6, "ASTC 6x6", 3.56f),
            (TextureImporterFormat.ASTC_8x8, "ASTC 8x8", 2f),
            (TextureImporterFormat.ASTC_10x10, "ASTC 10x10", 1.28f),
            (TextureImporterFormat.ASTC_12x12, "ASTC 12x12", 0.89f),
            (TextureImporterFormat.ETC2_RGB4, "ETC2 RGB 4bit", 4f),
            (TextureImporterFormat.ETC2_RGB4_PUNCHTHROUGH_ALPHA, "ETC2 RGB 4bit + 1bit A", 4f),
            (TextureImporterFormat.ETC2_RGBA8, "ETC2 RGBA 8bit", 8f),
            (TextureImporterFormat.ETC2_RGBA8Crunched, "ETC2 RGBA Crunched", 4f),
            (TextureImporterFormat.ETC_RGB4, "ETC RGB 4bit", 4f),
            (TextureImporterFormat.ETC_RGB4Crunched, "ETC RGB Crunched", 2f),
            (TextureImporterFormat.EAC_R, "EAC R", 4f),
            (TextureImporterFormat.EAC_RG, "EAC RG", 8f),
            (TextureImporterFormat.DXT1, "DXT1 (BC1)", 4f),
            (TextureImporterFormat.DXT5, "DXT5 (BC3)", 8f),
            (TextureImporterFormat.DXT1Crunched, "DXT1 Crunched", 2f),
            (TextureImporterFormat.DXT5Crunched, "DXT5 Crunched", 4f),
            (TextureImporterFormat.BC4, "BC4", 4f),
            (TextureImporterFormat.BC5, "BC5", 8f),
            (TextureImporterFormat.BC6H, "BC6H", 8f),
            (TextureImporterFormat.BC7, "BC7", 8f),
            (TextureImporterFormat.RGBA32, "RGBA 32bit", 32f),
            (TextureImporterFormat.ARGB32, "ARGB 32bit", 32f),
            (TextureImporterFormat.RGB24, "RGB 24bit", 24f),
            (TextureImporterFormat.RGBA16, "RGBA 16bit", 16f),
            (TextureImporterFormat.ARGB16, "ARGB 16bit", 16f),
            (TextureImporterFormat.RGB16, "RGB 16bit", 16f),
            (TextureImporterFormat.R16, "R 16bit", 16f),
            (TextureImporterFormat.R8, "R 8bit", 8f),
            (TextureImporterFormat.Alpha8, "Alpha 8", 8f),
            (TextureImporterFormat.RGBAHalf, "RGBA Half", 64f),
            (TextureImporterFormat.RGBAFloat, "RGBA Float", 128f)
        };

        private static readonly TextureImporterFormat[] Astc =
        {
            TextureImporterFormat.ASTC_4x4, TextureImporterFormat.ASTC_5x5, TextureImporterFormat.ASTC_6x6,
            TextureImporterFormat.ASTC_8x8, TextureImporterFormat.ASTC_10x10, TextureImporterFormat.ASTC_12x12
        };

        private static readonly TextureImporterFormat[] Etc =
        {
            TextureImporterFormat.ETC2_RGB4, TextureImporterFormat.ETC2_RGB4_PUNCHTHROUGH_ALPHA, TextureImporterFormat.ETC2_RGBA8,
            TextureImporterFormat.ETC2_RGBA8Crunched, TextureImporterFormat.ETC_RGB4, TextureImporterFormat.ETC_RGB4Crunched
        };

        private static readonly TextureImporterFormat[] Desktop =
        {
            TextureImporterFormat.DXT1, TextureImporterFormat.DXT5, TextureImporterFormat.DXT1Crunched, TextureImporterFormat.DXT5Crunched,
            TextureImporterFormat.BC4, TextureImporterFormat.BC5, TextureImporterFormat.BC6H, TextureImporterFormat.BC7
        };

        private static readonly TextureImporterFormat[] Uncompressed =
        {
            TextureImporterFormat.RGBA32, TextureImporterFormat.RGB24, TextureImporterFormat.RGBA16,
            TextureImporterFormat.RGB16, TextureImporterFormat.R8, TextureImporterFormat.Alpha8
        };

        private static readonly Dictionary<TextureImporterFormat, (string label, float bitsPerPixel)> ByFormat =
            Known.GroupBy(known => known.format).ToDictionary(group => group.Key, group => (group.First().label, group.First().bitsPerPixel));

        public static IReadOnlyList<TextureImporterFormat> ForPlatform(string platformName)
        {
            var formats = new List<TextureImporterFormat> { TextureImporterFormat.Automatic };
            switch (platformName)
            {
                case "Android":
                    formats.AddRange(Astc);
                    formats.AddRange(Etc);
                    break;
                case "iPhone":
                    formats.AddRange(Astc);
                    formats.AddRange(Etc.Where(format => format.ToString().StartsWith("ETC2", StringComparison.Ordinal)));
                    break;
                case "Standalone":
                    formats.AddRange(Desktop);
                    break;
                default:
                    formats.AddRange(Desktop);
                    formats.AddRange(Astc);
                    formats.AddRange(Etc);
                    break;
            }

            formats.AddRange(Uncompressed);
            return formats;
        }

        public static string Label(TextureImporterFormat format) =>
            ByFormat.TryGetValue(format, out var known) ? known.label : format.ToString();

        public static bool IsBlockCompressed(TextureImporterFormat format) =>
            Desktop.Contains(format) || Etc.Contains(format) || format is TextureImporterFormat.EAC_R or TextureImporterFormat.EAC_RG;

        public static bool IsCrunched(TextureImporterFormat format) => format.ToString().Contains("Crunched");

        public static (int width, int height) ImportedSize(int sourceWidth, int sourceHeight, int maxSize)
        {
            var width = sourceWidth;
            var height = sourceHeight;
            while (maxSize > 0 && Math.Max(width, height) > maxSize)
            {
                width = Math.Max(1, width / 2);
                height = Math.Max(1, height / 2);
            }

            return (width, height);
        }

        public static long EstimateBytes(int sourceWidth, int sourceHeight, int maxSize, bool mipmaps, TextureImporterFormat format)
        {
            if (!ByFormat.TryGetValue(format, out var known) || known.bitsPerPixel <= 0)
                return Unknown;

            var (width, height) = ImportedSize(sourceWidth, sourceHeight, maxSize);
            var bytes = (double)width * height * known.bitsPerPixel / 8d;
            if (mipmaps)
                bytes *= 4d / 3d;
            return (long)Math.Ceiling(bytes);
        }
    }
}
