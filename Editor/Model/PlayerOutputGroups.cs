using System;
using System.Collections.Generic;
using System.Linq;

namespace PBTruong.BuildAnalyzer.Editor
{
    public sealed class OutputGroup
    {
        public OutputGroup(string name, long size, int fileCount)
        {
            Name = name;
            Size = size;
            FileCount = fileCount;
        }

        public string Name { get; }
        public long Size { get; }
        public int FileCount { get; }
    }

    public sealed class OutputBreakdown
    {
        public OutputBreakdown(IReadOnlyList<OutputGroup> shipped, long intermediateSize, int intermediateCount)
        {
            Shipped = shipped;
            IntermediateSize = intermediateSize;
            IntermediateCount = intermediateCount;
        }

        public IReadOnlyList<OutputGroup> Shipped { get; }
        public long IntermediateSize { get; }
        public int IntermediateCount { get; }
    }

    public static class PlayerOutputGroups
    {
        private const string JniLibsMarker = "/jniLibs/";
        private const double MinorGroupShare = 0.002;

        private static readonly HashSet<string> IntermediateRoles = new(StringComparer.OrdinalIgnoreCase)
        {
            "cpp", "c", "h", "pdb", "ManagedLibrary", "DependentManagedLibrary", "ManagedEngineAPI"
        };

        private static readonly string[] IntermediateMarkers =
        {
            "/Il2CppBackup/", "/il2cppOutput/", "/symbols/", "_BackUpThisFolder_", "_DoNotShip"
        };

        public static OutputBreakdown Summarize(PlayerBuildData player)
        {
            var outputPath = Normalize(player.OutputPath);
            var files = player.Files.Where(file => Normalize(file.Path) != outputPath).ToList();
            var intermediate = files.Where(IsIntermediate).ToList();

            var groups = files.Except(intermediate)
                .GroupBy(GroupOf)
                .Select(group => new OutputGroup(group.Key, group.Sum(file => file.Size), group.Count()))
                .OrderByDescending(group => group.Size)
                .ToList();

            var minorThreshold = groups.Sum(group => group.Size) * MinorGroupShare;
            var shipped = groups.Where(group => group.Size >= minorThreshold).ToList();
            var minor = groups.Where(group => group.Size < minorThreshold).ToList();
            if (minor.Count > 0)
                shipped.Add(new OutputGroup("Other", minor.Sum(group => group.Size), minor.Sum(group => group.FileCount)));

            return new OutputBreakdown(shipped, intermediate.Sum(file => file.Size), intermediate.Count);
        }

        private static bool IsIntermediate(PlayerOutputFile file)
        {
            if (IntermediateRoles.Contains(file.Role))
                return true;
            var path = Normalize(file.Path);
            return IntermediateMarkers.Any(marker => path.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string GroupOf(PlayerOutputFile file)
        {
            var path = Normalize(file.Path);
            var jniLibs = path.IndexOf(JniLibsMarker, StringComparison.Ordinal);
            if (jniLibs >= 0)
                return $"Native libraries ({path.Substring(jniLibs + JniLibsMarker.Length).Split('/')[0]})";
            if (path.Contains("/assets/aa/"))
                return "Addressables bundles";
            if (path.Contains("/assets/bin/Data/"))
                return "Player data (bin/Data)";
            if (path.Contains("/src/main/assets/"))
                return "StreamingAssets & other assets";
            if (path.Contains("/src/main/res/"))
                return "Android resources";
            if (file.Role == "aar" || file.Role == "jar")
                return "Android libraries (aar/jar)";
            return string.IsNullOrEmpty(file.Role) ? "Other" : file.Role;
        }

        private static string Normalize(string path) => (path ?? string.Empty).Replace('\\', '/');
    }
}
