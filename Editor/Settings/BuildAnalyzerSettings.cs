using System.IO;
using UnityEditor;
using UnityEngine;

namespace PBTruong.BuildAnalyzer.Editor
{
    [FilePath("UserSettings/BuildAnalyzer.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class BuildAnalyzerSettings : ScriptableSingleton<BuildAnalyzerSettings>
    {
        private const long KiloByte = 1024;

        [SerializeField] private string _reportsFolder = "BuildAnalyzerReports";
        [SerializeField] private bool _autoSnapshotOnBuild;
        [SerializeField] private long _largeTextureThreshold = 512 * KiloByte;
        [SerializeField] private long _largeAudioThreshold = 256 * KiloByte;
        [SerializeField] private long _minDuplicateSize = 10 * KiloByte;
        [SerializeField] private long _minResourcesAssetSize = 64 * KiloByte;
        [SerializeField] private long _minUncompressedBundleSize = 64 * KiloByte;

        public string ReportsFolder => _reportsFolder;
        public string ReportsRoot => Path.GetFullPath(_reportsFolder);
        public bool AutoSnapshotOnBuild => _autoSnapshotOnBuild;
        public long LargeTextureThreshold => _largeTextureThreshold;
        public long LargeAudioThreshold => _largeAudioThreshold;
        public long MinDuplicateSize => _minDuplicateSize;
        public long MinResourcesAssetSize => _minResourcesAssetSize;
        public long MinUncompressedBundleSize => _minUncompressedBundleSize;

        public void Apply(
            string reportsFolder,
            bool autoSnapshotOnBuild,
            long largeTextureThreshold,
            long largeAudioThreshold,
            long minDuplicateSize,
            long minResourcesAssetSize,
            long minUncompressedBundleSize)
        {
            _reportsFolder = string.IsNullOrWhiteSpace(reportsFolder) ? "BuildAnalyzerReports" : reportsFolder.Trim();
            _autoSnapshotOnBuild = autoSnapshotOnBuild;
            _largeTextureThreshold = largeTextureThreshold;
            _largeAudioThreshold = largeAudioThreshold;
            _minDuplicateSize = minDuplicateSize;
            _minResourcesAssetSize = minResourcesAssetSize;
            _minUncompressedBundleSize = minUncompressedBundleSize;
            Save(true);
        }
    }
}
