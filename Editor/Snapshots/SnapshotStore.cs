using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PBTruong.BuildAnalyzer.Editor
{
    public sealed class SnapshotFileInfo
    {
        public SnapshotFileInfo(string path, string label, DateTime createdAt)
        {
            Path = path;
            Label = label;
            CreatedAt = createdAt;
        }

        public string Path { get; }
        public string Label { get; }
        public DateTime CreatedAt { get; }
    }

    public static class SnapshotStore
    {
        private const string FolderName = "Snapshots";
        private const string Separator = "__";
        private const string TimeFormat = "yyyyMMdd-HHmmss";
        private const string Extension = ".json";

        public static string Folder => Path.Combine(BuildAnalyzerSettings.instance.ReportsRoot, FolderName);

        public static string Save(BuildSnapshot snapshot)
        {
            Directory.CreateDirectory(Folder);
            var fileName = $"{snapshot.CreatedAt.ToString(TimeFormat, CultureInfo.InvariantCulture)}{Separator}{Sanitize(snapshot.Label)}{Extension}";
            var path = Path.Combine(Folder, fileName);
            File.WriteAllText(path, JsonUtility.ToJson(snapshot));
            return path;
        }

        public static BuildSnapshot Load(string path) => JsonUtility.FromJson<BuildSnapshot>(File.ReadAllText(path));

        public static IReadOnlyList<SnapshotFileInfo> List()
        {
            if (!Directory.Exists(Folder))
                return Array.Empty<SnapshotFileInfo>();

            return Directory.GetFiles(Folder, "*" + Extension)
                .Select(Describe)
                .OrderByDescending(info => info.CreatedAt)
                .ToList();
        }

        private static SnapshotFileInfo Describe(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var separatorIndex = name.IndexOf(Separator, StringComparison.Ordinal);
            var stamp = separatorIndex < 0 ? name : name.Substring(0, separatorIndex);
            var label = separatorIndex < 0 ? name : name.Substring(separatorIndex + Separator.Length);
            var createdAt = DateTime.TryParseExact(stamp, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : File.GetLastWriteTime(path);
            return new SnapshotFileInfo(path, label, createdAt);
        }

        private static string Sanitize(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return "snapshot";

            var invalid = Path.GetInvalidFileNameChars();
            return new string(label.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }
    }
}
