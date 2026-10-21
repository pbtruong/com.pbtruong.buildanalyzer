using System;
using System.Collections.Generic;
using System.Linq;

namespace PBTruong.BuildAnalyzer.Editor
{
    public static class SnapshotComparer
    {
        public static SnapshotDiff Compare(BuildSnapshot baseline, BuildSnapshot target)
        {
            var baselineByKey = IndexByKey(baseline.Entries);
            var targetByKey = IndexByKey(target.Entries);

            var entries = new List<SizeDiffEntry>(Math.Max(baselineByKey.Count, targetByKey.Count));
            foreach (var key in baselineByKey.Keys.Union(targetByKey.Keys))
            {
                baselineByKey.TryGetValue(key, out var before);
                targetByKey.TryGetValue(key, out var after);
                var reference = after ?? before;
                var beforeSize = before?.Size ?? 0;
                var afterSize = after?.Size ?? 0;
                entries.Add(new SizeDiffEntry(reference.AssetPath, reference.Source, reference.Category, beforeSize, afterSize, Classify(before, after, beforeSize, afterSize)));
            }

            var categories = Enum.GetValues(typeof(AssetCategory)).Cast<AssetCategory>()
                .Select(category => new CategoryDelta(
                    category,
                    entries.Where(entry => entry.Category == category).Sum(entry => entry.BaselineSize),
                    entries.Where(entry => entry.Category == category).Sum(entry => entry.TargetSize)))
                .Where(delta => delta.Baseline != 0 || delta.Target != 0)
                .ToList();

            return new SnapshotDiff(entries, categories, baseline.AssetsSize, target.AssetsSize);
        }

        private static DiffStatus Classify(SizeEntry before, SizeEntry after, long beforeSize, long afterSize)
        {
            if (before == null)
                return DiffStatus.Added;
            if (after == null)
                return DiffStatus.Removed;
            if (afterSize > beforeSize)
                return DiffStatus.Grew;
            return afterSize < beforeSize ? DiffStatus.Shrank : DiffStatus.Unchanged;
        }

        private static Dictionary<string, SizeEntry> IndexByKey(IEnumerable<SizeEntry> entries)
        {
            var index = new Dictionary<string, SizeEntry>();
            foreach (var entry in entries)
            {
                index[entry.Key] = index.TryGetValue(entry.Key, out var existing)
                    ? new SizeEntry(entry.AssetPath, entry.TypeName, entry.Category, existing.Size + entry.Size, entry.Source, existing.Container, existing.IsImplicit && entry.IsImplicit)
                    : entry;
            }

            return index;
        }
    }
}
