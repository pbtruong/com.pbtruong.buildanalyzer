using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace PBTruong.BuildAnalyzer.Editor
{
    [Serializable]
    public sealed class BuildSnapshot
    {
        [SerializeField] private string _label;
        [SerializeField] private string _createdAt;
        [SerializeField] private string _platform;
        [SerializeField] private long _playerOutputSize;
        [SerializeField] private long _bundlesCompressedSize;
        [SerializeField] private List<SizeEntry> _entries;

        public BuildSnapshot(string label, DateTime createdAt, string platform, long playerOutputSize, long bundlesCompressedSize, List<SizeEntry> entries)
        {
            _label = label;
            _createdAt = createdAt.ToString("o", CultureInfo.InvariantCulture);
            _platform = platform;
            _playerOutputSize = playerOutputSize;
            _bundlesCompressedSize = bundlesCompressedSize;
            _entries = entries;
        }

        public string Label => _label;
        public DateTime CreatedAt => DateTime.Parse(_createdAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        public string Platform => _platform;
        public long PlayerOutputSize => _playerOutputSize;
        public long BundlesCompressedSize => _bundlesCompressedSize;
        public IReadOnlyList<SizeEntry> Entries => _entries;
        public long AssetsSize => _entries.Sum(entry => entry.Size);
    }

    public enum DiffStatus
    {
        Added,
        Removed,
        Grew,
        Shrank,
        Unchanged
    }

    public sealed class SizeDiffEntry
    {
        public SizeDiffEntry(string assetPath, SizeSource source, AssetCategory category, long baselineSize, long targetSize, DiffStatus status)
        {
            AssetPath = assetPath;
            Source = source;
            Category = category;
            BaselineSize = baselineSize;
            TargetSize = targetSize;
            Status = status;
        }

        public string AssetPath { get; }
        public SizeSource Source { get; }
        public AssetCategory Category { get; }
        public long BaselineSize { get; }
        public long TargetSize { get; }
        public DiffStatus Status { get; }
        public long Delta => TargetSize - BaselineSize;
        public string Name => AssetPaths.DisplayName(AssetPath);
    }

    public sealed class CategoryDelta
    {
        public CategoryDelta(AssetCategory category, long baseline, long target)
        {
            Category = category;
            Baseline = baseline;
            Target = target;
        }

        public AssetCategory Category { get; }
        public long Baseline { get; }
        public long Target { get; }
        public long Delta => Target - Baseline;
    }

    public sealed class SnapshotDiff
    {
        public SnapshotDiff(IReadOnlyList<SizeDiffEntry> entries, IReadOnlyList<CategoryDelta> categories, long baselineTotal, long targetTotal)
        {
            Entries = entries;
            Categories = categories;
            BaselineTotal = baselineTotal;
            TargetTotal = targetTotal;
        }

        public IReadOnlyList<SizeDiffEntry> Entries { get; }
        public IReadOnlyList<CategoryDelta> Categories { get; }
        public long BaselineTotal { get; }
        public long TargetTotal { get; }
        public long Delta => TargetTotal - BaselineTotal;

        public int Count(DiffStatus status) => Entries.Count(entry => entry.Status == status);
        public long DeltaOf(DiffStatus status) => Entries.Where(entry => entry.Status == status).Sum(entry => entry.Delta);
    }
}
