using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace PBTruong.BuildAnalyzer.Editor
{
    public static class InsightAnalyzer
    {
        public static IReadOnlyList<SizeInsight> Run(PlayerBuildData player, BundleLayoutData layout)
        {
            if (player == null && layout == null)
                return Array.Empty<SizeInsight>();

            var context = new InsightContext(player, layout, BuildPlatforms.Resolve(player, layout), BuildAnalyzerSettings.instance);
            return CreateRules()
                .SelectMany(rule => rule.Evaluate(context))
                .OrderByDescending(insight => insight.Severity)
                .ThenByDescending(insight => insight.Impact)
                .ToList();
        }

        public static IReadOnlyList<ISizeInsightRule> CreateRules() =>
            TypeCache.GetTypesDerivedFrom<ISizeInsightRule>()
                .Where(type => !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null)
                .Select(type => (ISizeInsightRule)Activator.CreateInstance(type))
                .OrderBy(rule => rule.Name)
                .ToList();
    }
}
