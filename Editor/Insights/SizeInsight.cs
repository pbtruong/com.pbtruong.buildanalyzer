using System;
using System.Collections.Generic;
using System.Linq;

namespace PBTruong.BuildAnalyzer.Editor
{
    public enum InsightSeverity
    {
        Info,
        Warning,
        Critical
    }

    public sealed class SizeInsight
    {
        public SizeInsight(string rule, InsightSeverity severity, string assetPath, string problem, string suggestion, long impact)
        {
            Rule = rule;
            Severity = severity;
            AssetPath = assetPath;
            Problem = problem;
            Suggestion = suggestion;
            Impact = impact;
        }

        public string Rule { get; }
        public InsightSeverity Severity { get; }
        public string AssetPath { get; }
        public string Problem { get; }
        public string Suggestion { get; }
        public long Impact { get; }
        public string Name => AssetPaths.DisplayName(AssetPath);
    }

    public sealed class InsightContext
    {
        public InsightContext(PlayerBuildData player, BundleLayoutData layout, string platformName, BuildAnalyzerSettings settings)
        {
            Player = player;
            Layout = layout;
            PlatformName = platformName;
            Settings = settings;
            Assets = MergeBySourcePath(player, layout);
        }

        public PlayerBuildData Player { get; }
        public BundleLayoutData Layout { get; }
        public string PlatformName { get; }
        public BuildAnalyzerSettings Settings { get; }
        public IReadOnlyList<SizeEntry> Assets { get; }

        internal static IReadOnlyList<SizeEntry> MergeBySourcePath(PlayerBuildData player, BundleLayoutData layout)
        {
            var all = (player?.Assets ?? Array.Empty<SizeEntry>()).Concat(layout?.Assets ?? Array.Empty<SizeEntry>());
            return all.GroupBy(entry => entry.AssetPath)
                .Select(group => group.Count() == 1 ? group.First() : Combine(group))
                .ToList();
        }

        private static SizeEntry Combine(IGrouping<string, SizeEntry> group)
        {
            var largest = group.OrderByDescending(entry => entry.Size).First();
            return new SizeEntry(
                group.Key,
                largest.TypeName,
                largest.Category,
                group.Sum(entry => entry.Size),
                largest.Source,
                string.Join(SizeEntry.ContainerSeparator, group.Select(entry => entry.Container)),
                group.All(entry => entry.IsImplicit));
        }
    }

    public interface ISizeInsightRule
    {
        string Name { get; }
        IEnumerable<SizeInsight> Evaluate(InsightContext context);
    }
}
