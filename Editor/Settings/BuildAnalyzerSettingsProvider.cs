using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal static class BuildAnalyzerSettingsProvider
    {
        public const string SettingsPath = "Preferences/PBTruong/Build Analyzer";
        private const long KiloByte = 1024;

        [SettingsProvider]
        private static SettingsProvider Create() => new(SettingsPath, SettingsScope.User)
        {
            guiHandler = _ => Draw(),
            keywords = new HashSet<string> { "build", "size", "snapshot", "addressables", "texture", "audio" }
        };

        private static void Draw()
        {
            var settings = BuildAnalyzerSettings.instance;
            EditorGUIUtility.labelWidth = 260;
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Storage", EditorStyles.boldLabel);
            var folder = EditorGUILayout.TextField(new GUIContent("Reports Folder", "Relative to the project root."), settings.ReportsFolder);
            var autoSnapshot = EditorGUILayout.Toggle(new GUIContent("Auto Snapshot After Player Build"), settings.AutoSnapshotOnBuild);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Insight Thresholds (KB)", EditorStyles.boldLabel);
            var texture = KiloField("Large Texture", settings.LargeTextureThreshold);
            var audio = KiloField("Large Audio Clip", settings.LargeAudioThreshold);
            var duplicate = KiloField("Min Duplicate Waste", settings.MinDuplicateSize);
            var resources = KiloField("Min Resources Asset", settings.MinResourcesAssetSize);
            var bundle = KiloField("Min Uncompressed Bundle", settings.MinUncompressedBundleSize);

            if (EditorGUI.EndChangeCheck())
                settings.Apply(folder, autoSnapshot, texture, audio, duplicate, resources, bundle);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox($"Snapshots and archived player reports are stored in:\n{settings.ReportsRoot}", MessageType.None);
        }

        private static long KiloField(string label, long bytes) =>
            System.Math.Max(0, EditorGUILayout.LongField(label, bytes / KiloByte)) * KiloByte;
    }
}
