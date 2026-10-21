using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace PBTruong.BuildAnalyzer.Editor.Tests
{
    public sealed class TextureCompressionTests
    {
        [Test]
        public void ImportedSize_HalvesUntilWithinMaxSize()
        {
            Assert.That(TextureFormats.ImportedSize(3000, 1500, 2048), Is.EqualTo((1500, 750)));
            Assert.That(TextureFormats.ImportedSize(1024, 512, 2048), Is.EqualTo((1024, 512)));
            Assert.That(TextureFormats.ImportedSize(4096, 4096, 512), Is.EqualTo((512, 512)));
        }

        [Test]
        public void EstimateBytes_UsesBitsPerPixelAndMipmaps()
        {
            Assert.That(TextureFormats.EstimateBytes(1024, 1024, 2048, false, TextureImporterFormat.RGBA32), Is.EqualTo(4L * 1024 * 1024));
            Assert.That(TextureFormats.EstimateBytes(1024, 1024, 2048, true, TextureImporterFormat.RGBA32), Is.EqualTo(4L * 1024 * 1024 * 4 / 3 + 1));
            Assert.That(TextureFormats.EstimateBytes(1024, 1024, 2048, false, TextureImporterFormat.ASTC_8x8), Is.EqualTo(256L * 1024));
            Assert.That(TextureFormats.EstimateBytes(1024, 1024, 2048, false, TextureImporterFormat.Automatic), Is.EqualTo(TextureFormats.Unknown));
        }

        [Test]
        public void ForPlatform_AndroidOffersAstcAndEtcButNoDesktopFormats()
        {
            var formats = TextureFormats.ForPlatform("Android");

            Assert.That(formats, Has.Member(TextureImporterFormat.ASTC_6x6));
            Assert.That(formats, Has.Member(TextureImporterFormat.ETC2_RGBA8));
            Assert.That(formats, Has.No.Member(TextureImporterFormat.DXT5));
            Assert.That(formats.First(), Is.EqualTo(TextureImporterFormat.Automatic));
        }

        [Test]
        public void Plan_WithoutChangesKeepsSettings()
        {
            var plan = new TextureCompressionPlan("Android");
            var settings = new TextureImporterPlatformSettings { name = "Android", overridden = false, format = TextureImporterFormat.Automatic, maxTextureSize = 2048 };
            var before = Clone(settings);

            plan.ApplyTo(settings);

            Assert.That(plan.HasChanges, Is.False);
            Assert.That(TextureCompressionPlan.SameSettings(before, settings), Is.True);
        }

        [Test]
        public void Plan_FormatChangeEnablesOverride()
        {
            var plan = new TextureCompressionPlan("Android") { Format = TextureImporterFormat.ASTC_6x6, MaxSize = 1024 };
            var settings = new TextureImporterPlatformSettings { name = "Android", overridden = false, maxTextureSize = 2048 };

            plan.ApplyTo(settings);

            Assert.That(settings.overridden, Is.True);
            Assert.That(settings.format, Is.EqualTo(TextureImporterFormat.ASTC_6x6));
            Assert.That(settings.maxTextureSize, Is.EqualTo(1024));
        }

        [Test]
        public void Plan_RemoveOverrideIgnoresOtherFields()
        {
            var plan = new TextureCompressionPlan("iPhone") { Override = OverrideChange.Remove, MaxSize = 256 };
            var settings = new TextureImporterPlatformSettings { name = "iPhone", overridden = true, format = TextureImporterFormat.ASTC_4x4, maxTextureSize = 2048 };

            plan.ApplyTo(settings);

            Assert.That(settings.overridden, Is.False);
            Assert.That(settings.maxTextureSize, Is.EqualTo(2048));
        }

        [Test]
        public void Plan_DefaultPlatformChangesCompressionOnly()
        {
            var plan = new TextureCompressionPlan(BuildPlatforms.DefaultName) { Compression = TextureImporterCompression.CompressedHQ, Crunched = true };
            var settings = new TextureImporterPlatformSettings { name = BuildPlatforms.DefaultName, textureCompression = TextureImporterCompression.Compressed };

            plan.ApplyTo(settings);

            Assert.That(settings.textureCompression, Is.EqualTo(TextureImporterCompression.CompressedHQ));
            Assert.That(settings.crunchedCompression, Is.True);
            Assert.That(settings.overridden, Is.False);
        }

        [Test]
        public void ParsePackableGuids_ReadsSpriteAtlasV2Packables()
        {
            var yaml = new[]
            {
                "SpriteAtlasAsset:",
                "  m_ImporterData:",
                "    packables:",
                "    - {fileID: 21300000, guid: EFDAEBEA0045b9142a65b49d72ff2e28, type: 3}",
                "    - {fileID: 102900000, guid: c432a704e9eb3754981ca11e14d5e52c, type: 3}",
                "  m_IsVariant: 0",
                "    - {fileID: 1, guid: 00000000000000000000000000000001, type: 3}"
            };

            Assert.That(SpriteAtlasMembership.ParsePackableGuids(yaml),
                Is.EqualTo(new[] { "efdaebea0045b9142a65b49d72ff2e28", "c432a704e9eb3754981ca11e14d5e52c" }));
        }

        private static TextureImporterPlatformSettings Clone(TextureImporterPlatformSettings source)
        {
            var copy = new TextureImporterPlatformSettings();
            source.CopyTo(copy);
            return copy;
        }
    }
}
