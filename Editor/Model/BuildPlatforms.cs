using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;

namespace PBTruong.BuildAnalyzer.Editor
{
    public sealed class BuildPlatform
    {
        public BuildPlatform(string name, string label, BuildTarget target)
        {
            Name = name;
            Label = label;
            Target = target;
        }

        public string Name { get; }
        public string Label { get; }
        public BuildTarget Target { get; }
        public bool IsDefault => Name == BuildPlatforms.DefaultName;
    }

    public static class BuildPlatforms
    {
        public const string DefaultName = "DefaultTexturePlatform";

        private static readonly (BuildTargetGroup group, BuildTarget target, string label)[] Candidates =
        {
            (BuildTargetGroup.Android, BuildTarget.Android, "Android"),
            (BuildTargetGroup.iOS, BuildTarget.iOS, "iOS"),
            (BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64, "Standalone"),
            (BuildTargetGroup.WebGL, BuildTarget.WebGL, "WebGL")
        };

        public static string NameOf(BuildTarget target) =>
            NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target)).TargetName;

        public static string Resolve(PlayerBuildData player, BundleLayoutData layout)
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            if (player != null)
                target = player.Platform;
            else if (layout != null && Enum.TryParse(layout.Platform, out BuildTarget layoutTarget))
                target = layoutTarget;

            return NameOf(target);
        }

        public static IReadOnlyList<BuildPlatform> Installed()
        {
            var active = EditorUserBuildSettings.activeBuildTarget;
            var platforms = new List<BuildPlatform> { new(DefaultName, "Default", active) };
            foreach (var (group, target, label) in Candidates)
            {
                if (BuildPipeline.IsBuildTargetSupported(group, target))
                    platforms.Add(new BuildPlatform(NamedBuildTarget.FromBuildTargetGroup(group).TargetName, label, target));
            }

            return platforms;
        }
    }
}
