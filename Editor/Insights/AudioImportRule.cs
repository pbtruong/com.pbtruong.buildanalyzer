using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class AudioImportRule : ISizeInsightRule
    {
        private const float LongClipSeconds = 5f;

        public string Name => "Audio Import";

        public IEnumerable<SizeInsight> Evaluate(InsightContext context)
        {
            foreach (var entry in context.Assets)
            {
                if (entry.Category != AssetCategory.Audio || entry.Size < context.Settings.LargeAudioThreshold)
                    continue;
                if (AssetImporter.GetAtPath(entry.AssetPath) is not AudioImporter importer)
                    continue;

                var insight = Inspect(entry, importer, context.PlatformName);
                if (insight != null)
                    yield return insight;
            }
        }

        private SizeInsight Inspect(SizeEntry entry, AudioImporter importer, string platform)
        {
            var sample = importer.ContainsSampleSettingsOverride(platform)
                ? importer.GetOverrideSampleSettings(platform)
                : importer.defaultSampleSettings;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(entry.AssetPath);
            var length = clip != null ? clip.length : 0f;
            var channels = clip != null ? clip.channels : 1;

            var severity = InsightSeverity.Info;
            var problems = new List<string>();
            var fixes = new List<string>();

            if (sample.compressionFormat == AudioCompressionFormat.PCM)
            {
                severity = InsightSeverity.Critical;
                problems.Add("PCM (uncompressed)");
                fixes.Add("Use Vorbis");
            }
            else if (sample.compressionFormat == AudioCompressionFormat.ADPCM && length > LongClipSeconds)
            {
                severity = InsightSeverity.Warning;
                problems.Add($"ADPCM on a {length:0.#}s clip");
                fixes.Add("Use Vorbis for long clips");
            }

            if (channels > 1 && !importer.forceToMono)
            {
                problems.Add("stereo");
                fixes.Add("Enable Force To Mono if stereo is not needed");
            }

            if (sample.loadType == AudioClipLoadType.DecompressOnLoad && length > LongClipSeconds)
            {
                severity = severity < InsightSeverity.Warning ? InsightSeverity.Warning : severity;
                problems.Add("Decompress On Load (runtime memory)");
                fixes.Add("Use Compressed In Memory or Streaming");
            }

            if (problems.Count == 0)
                return null;

            problems.Insert(0, $"{length:0.#}s, {sample.compressionFormat}, {sample.loadType}");
            return new SizeInsight(Name, severity, entry.AssetPath, string.Join("; ", problems), string.Join(". ", fixes), entry.Size);
        }
    }
}
