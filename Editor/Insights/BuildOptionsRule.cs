using System;
using System.Collections.Generic;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class BuildOptionsRule : ISizeInsightRule
    {
        public string Name => "Build Options";

        public IEnumerable<SizeInsight> Evaluate(InsightContext context)
        {
            var player = context.Player;
            if (player == null || !player.IsDevelopment)
                return Array.Empty<SizeInsight>();

            return new[]
            {
                new SizeInsight(
                    Name,
                    InsightSeverity.Warning,
                    player.OutputPath,
                    $"Development build ({player.Options}): debug code and symbols make it larger than a release build",
                    "Measure size on a non-development build before optimizing",
                    player.OutputSize)
            };
        }
    }
}
