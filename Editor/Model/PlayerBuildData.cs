using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace PBTruong.BuildAnalyzer.Editor
{
    public sealed class PlayerOutputFile
    {
        public PlayerOutputFile(string path, string role, long size)
        {
            Path = path;
            Role = role;
            Size = size;
        }

        public string Path { get; }
        public string Role { get; }
        public long Size { get; }
    }

    public sealed class PlayerBuildData
    {
        public PlayerBuildData(
            string sourceLabel,
            BuildTarget platform,
            DateTime buildStartedAt,
            TimeSpan totalTime,
            BuildResult result,
            BuildOptions options,
            long reportedSize,
            string outputPath,
            long outputFileSize,
            IReadOnlyList<SizeEntry> assets,
            IReadOnlyList<PlayerOutputFile> files,
            IReadOnlyList<string> includedModules,
            IReadOnlyDictionary<string, string[]> scenesUsingAsset)
        {
            SourceLabel = sourceLabel;
            Platform = platform;
            BuildStartedAt = buildStartedAt;
            TotalTime = totalTime;
            Result = result;
            Options = options;
            ReportedSize = reportedSize;
            OutputPath = outputPath;
            OutputFileSize = outputFileSize;
            Assets = assets;
            Files = files;
            IncludedModules = includedModules;
            ScenesUsingAsset = scenesUsingAsset;
            AssetsSize = assets.Sum(entry => entry.Size);
        }

        public string SourceLabel { get; }
        public BuildTarget Platform { get; }
        public DateTime BuildStartedAt { get; }
        public TimeSpan TotalTime { get; }
        public BuildResult Result { get; }
        public BuildOptions Options { get; }
        public long ReportedSize { get; }
        public string OutputPath { get; }
        public long OutputFileSize { get; }
        public bool HasOutputFile => OutputFileSize >= 0;
        public long OutputSize => HasOutputFile ? OutputFileSize : ReportedSize;
        public bool IsDevelopment => (Options & BuildOptions.Development) != 0;
        public IReadOnlyList<SizeEntry> Assets { get; }
        public IReadOnlyList<PlayerOutputFile> Files { get; }
        public IReadOnlyList<string> IncludedModules { get; }
        public IReadOnlyDictionary<string, string[]> ScenesUsingAsset { get; }
        public long AssetsSize { get; }
    }
}
