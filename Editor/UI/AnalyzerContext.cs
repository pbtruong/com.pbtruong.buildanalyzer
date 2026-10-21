using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class AnalyzerContext
    {
        private const string ProgressTitle = "Build Analyzer";

        private IReadOnlyList<SizeInsight> _insights;
        private IReadOnlyDictionary<string, SizeEntry> _buildSizes;
        private string _pinnedPlayerReport;
        private string _pinnedLayout;

        public AnalyzerContext()
        {
            LayoutProvider = BundleLayoutProviderLocator.Find();
        }

        public event Action Changed;
        public event Action SnapshotsChanged;

        public IBundleLayoutProvider LayoutProvider { get; }
        public PlayerBuildData Player { get; private set; }
        public BundleLayoutData Layout { get; private set; }
        public IReadOnlyList<string> ArchivedPlayerReports { get; private set; } = Array.Empty<string>();
        public IReadOnlyList<LayoutFileInfo> LayoutFiles { get; private set; } = Array.Empty<LayoutFileInfo>();
        public string PinnedPlayerReport => _pinnedPlayerReport;
        public string PinnedLayout => _pinnedLayout;
        public bool HasData => Player != null || Layout != null;

        public string DefaultSnapshotLabel =>
            $"{(Player != null ? Player.Platform.ToString() : Layout?.Platform ?? "Build")} {PlayerSettings.bundleVersion}";

        public IReadOnlyList<SizeInsight> Insights
        {
            get
            {
                if (_insights != null)
                    return _insights;

                try
                {
                    EditorUtility.DisplayProgressBar(ProgressTitle, "Analyzing import settings...", 0.5f);
                    _insights = InsightAnalyzer.Run(Player, Layout);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }

                return _insights;
            }
        }

        public IReadOnlyDictionary<string, SizeEntry> BuildSizes =>
            _buildSizes ??= InsightContext.MergeBySourcePath(Player, Layout).ToDictionary(entry => entry.AssetPath);

        public string PlatformName => BuildPlatforms.Resolve(Player, Layout);

        public void Reload()
        {
            try
            {
                EditorUtility.DisplayProgressBar(ProgressTitle, "Reading player build report...", 0.2f);
                LoadPlayer();
                EditorUtility.DisplayProgressBar(ProgressTitle, "Reading Addressables build layout...", 0.6f);
                LoadLayout();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            NotifyChanged();
        }

        public void SelectPlayerReport(string pathOrLatest)
        {
            _pinnedPlayerReport = pathOrLatest;
            LoadPlayer();
            NotifyChanged();
        }

        public void SelectLayout(string pathOrLatest)
        {
            _pinnedLayout = pathOrLatest;
            try
            {
                EditorUtility.DisplayProgressBar(ProgressTitle, "Reading Addressables build layout...", 0.5f);
                LoadLayout();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            NotifyChanged();
        }

        public void SetLayoutGeneration(bool enabled)
        {
            LayoutProvider.IsLayoutGenerationEnabled = enabled;
            Changed?.Invoke();
        }

        public void InvalidateInsights() => _insights = null;

        public BuildSnapshot CreateSnapshot(string label) => SnapshotBuilder.Build(label, Player, Layout);

        public string SaveSnapshot(string label)
        {
            var path = SnapshotStore.Save(CreateSnapshot(label));
            SnapshotsChanged?.Invoke();
            return path;
        }

        public BundleLayoutData LoadLayoutFile(string path) => LayoutProvider?.Load(path);

        private void LoadPlayer()
        {
            ArchivedPlayerReports = PlayerReportArchive.FindArchived();
            Player = _pinnedPlayerReport == null
                ? PlayerReportReader.LoadLatest()
                : PlayerReportReader.LoadFromFile(_pinnedPlayerReport);
        }

        private void LoadLayout()
        {
            LayoutFiles = LayoutProvider?.FindLayoutFiles() ?? Array.Empty<LayoutFileInfo>();
            var path = _pinnedLayout ?? LayoutFiles.FirstOrDefault()?.Path;
            Layout = path != null ? LayoutProvider?.Load(path) : null;
        }

        private void NotifyChanged()
        {
            _insights = null;
            _buildSizes = null;
            Changed?.Invoke();
        }
    }
}
