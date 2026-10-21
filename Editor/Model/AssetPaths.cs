using System;

namespace PBTruong.BuildAnalyzer.Editor
{
    public static class AssetPaths
    {
        public const string BuiltInPrefix = "Built-in ";
        private const string BuiltInNameSeparator = ": ";

        public static bool IsProjectAsset(string path) =>
            path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal);

        public static string DisplayName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            if (path.StartsWith(BuiltInPrefix, StringComparison.Ordinal))
            {
                var separator = path.IndexOf(BuiltInNameSeparator, StringComparison.Ordinal);
                return separator < 0 ? path : path.Substring(separator + BuiltInNameSeparator.Length);
            }

            var slash = path.LastIndexOf('/');
            return slash < 0 ? path : path.Substring(slash + 1);
        }

        public static string NormalizeBuiltIn(string path)
        {
            var trimmed = path.TrimEnd();
            return trimmed.StartsWith(BuiltInPrefix, StringComparison.Ordinal) && trimmed.EndsWith(":", StringComparison.Ordinal)
                ? trimmed.TrimEnd(':') + " (unnamed)"
                : path;
        }
    }
}
