using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace PBTruong.BuildAnalyzer.Editor
{
    public static class PlayerReportArchive
    {
        private const string LatestReportPath = "Library/LastBuild.buildreport";
        private const string FolderName = "Player";
        private const string Extension = ".buildreport";

        public static string Folder => Path.Combine(BuildAnalyzerSettings.instance.ReportsRoot, FolderName);

        public static string ArchiveLatest(BuildTarget platform)
        {
            if (!File.Exists(LatestReportPath))
                return null;

            Directory.CreateDirectory(Folder);
            var destination = Path.Combine(Folder, $"{DateTime.Now:yyyyMMdd-HHmmss}__{platform}{Extension}");
            File.Copy(LatestReportPath, destination, true);
            return destination;
        }

        public static IReadOnlyList<string> FindArchived() =>
            Directory.Exists(Folder)
                ? Directory.GetFiles(Folder, "*" + Extension).OrderByDescending(path => path).ToList()
                : Array.Empty<string>();
    }
}
