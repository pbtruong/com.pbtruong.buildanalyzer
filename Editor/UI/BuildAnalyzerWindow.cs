using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PBTruong.BuildAnalyzer.Editor
{
    public sealed class BuildAnalyzerWindow : EditorWindow
    {
        private const string StyleSheetPath = "Packages/com.pbtruong.buildanalyzer/Editor/UI/BuildAnalyzer.uss";
        private const int RecentLayoutCount = 10;

        [SerializeField] private int _selectedTab;

        private readonly List<IAnalyzerTab> _tabs = new();
        private readonly List<Button> _tabButtons = new();
        private readonly HashSet<IAnalyzerTab> _staleTabs = new();
        private AnalyzerContext _context;
        private VisualElement _content;
        private ToolbarMenu _playerMenu;
        private ToolbarMenu _layoutMenu;
        private ToolbarButton _snapshotButton;
        private ToolbarButton _exportButton;

        private string _pendingUsagePath;

        [MenuItem("Window/Analysis/Build Analyzer")]
        public static void Open() => OpenWindow();

        [MenuItem("Assets/Find Usages In Build", false, 30)]
        private static void FindUsagesOfSelection() =>
            ShowUsages(AssetDatabase.GetAssetPath(Selection.activeObject));

        [MenuItem("Assets/Find Usages In Build", true)]
        private static bool CanFindUsagesOfSelection() =>
            Selection.activeObject != null && AssetDatabase.Contains(Selection.activeObject) && !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(Selection.activeObject));

        public static void ShowUsages(string assetPath)
        {
            var window = OpenWindow();
            if (window._content == null)
            {
                window._pendingUsagePath = assetPath;
                return;
            }

            window.FocusUsages(assetPath);
        }

        private static BuildAnalyzerWindow OpenWindow()
        {
            var window = GetWindow<BuildAnalyzerWindow>();
            window.titleContent = new GUIContent("Build Analyzer");
            window.minSize = new Vector2(760, 420);
            return window;
        }

        private void FocusUsages(string assetPath)
        {
            var index = _tabs.FindIndex(tab => tab is UsagesTab);
            _staleTabs.Remove(_tabs[index]);
            SelectTab(index);
            ((UsagesTab)_tabs[index]).Show(assetPath);
        }

        private void CreateGUI()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);
            rootVisualElement.AddToClassList("ba-root");

            _context = new AnalyzerContext();
            _context.Changed += OnContextChanged;
            _context.SnapshotsChanged += OnContextChanged;
            _tabs.Clear();
            _tabs.Add(new OverviewTab(_context));
            _tabs.Add(new PlayerAssetsTab(_context));
            _tabs.Add(new AddressablesTab(_context));
            _tabs.Add(new InsightsTab(_context));
            _tabs.Add(new CompareTab(_context));
            _tabs.Add(new UsagesTab(_context));
            _tabs.Add(new TexturesTab(_context));

            rootVisualElement.Add(CreateToolbar());
            rootVisualElement.Add(CreateTabBar());
            _content = Ui.Container("ba-content");
            rootVisualElement.Add(_content);

            _selectedTab = Mathf.Clamp(_selectedTab, 0, _tabs.Count - 1);
            _context.Reload();
            SelectTab(_selectedTab);

            if (_pendingUsagePath != null)
            {
                FocusUsages(_pendingUsagePath);
                _pendingUsagePath = null;
            }
        }

        private void OnDestroy()
        {
            if (_context != null)
            {
                _context.Changed -= OnContextChanged;
                _context.SnapshotsChanged -= OnContextChanged;
            }
        }

        private VisualElement CreateToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("ba-toolbar");

            _playerMenu = new ToolbarMenu { tooltip = "Player build report to inspect" };
            toolbar.Add(_playerMenu);

            _layoutMenu = new ToolbarMenu { tooltip = "Addressables build layout to inspect" };
            if (_context.LayoutProvider != null)
                toolbar.Add(_layoutMenu);

            toolbar.Add(new ToolbarButton(_context.Reload) { text = "Reload", tooltip = "Re-read the latest build data from disk" });
            toolbar.Add(Ui.Container("ba-toolbar__spacer"));

            _snapshotButton = new ToolbarButton { text = "Save Snapshot", tooltip = "Store the loaded data to compare with future builds" };
            _snapshotButton.clicked += ShowSnapshotPopup;
            toolbar.Add(_snapshotButton);

            _exportButton = new ToolbarButton(ExportCsv) { text = "Export CSV", tooltip = "Export the current table" };
            toolbar.Add(_exportButton);

            toolbar.Add(new ToolbarButton(() => SettingsService.OpenUserPreferences(BuildAnalyzerSettingsProvider.SettingsPath)) { text = "Settings" });
            return toolbar;
        }

        private VisualElement CreateTabBar()
        {
            var bar = Ui.Container("ba-tabbar");
            for (var i = 0; i < _tabs.Count; i++)
            {
                var index = i;
                var button = new Button(() => SelectTab(index)) { text = _tabs[i].Title };
                button.AddToClassList("ba-tab");
                _tabButtons.Add(button);
                bar.Add(button);
            }

            return bar;
        }

        private void SelectTab(int index)
        {
            _selectedTab = index;
            for (var i = 0; i < _tabButtons.Count; i++)
                _tabButtons[i].EnableInClassList("ba-tab--selected", i == index);

            var tab = _tabs[index];
            _content.Clear();
            _content.Add(tab.Root);
            if (_staleTabs.Remove(tab))
                tab.Refresh();
            _exportButton.SetEnabled(tab.CanExport && _context.HasData);
        }

        private void OnContextChanged()
        {
            RebuildPlayerMenu();
            RebuildLayoutMenu();
            _snapshotButton.SetEnabled(_context.HasData);

            foreach (var tab in _tabs)
                _staleTabs.Add(tab);
            SelectTab(_selectedTab);
        }

        private void RebuildPlayerMenu()
        {
            var pinned = _context.PinnedPlayerReport;
            _playerMenu.text = _context.Player == null
                ? "Player: none"
                : $"Player: {(pinned == null ? PlayerReportReader.LatestLabel : Path.GetFileNameWithoutExtension(pinned))}";

            var menu = _playerMenu.menu;
            menu.ClearItems();
            menu.AppendAction(PlayerReportReader.LatestLabel, _ => _context.SelectPlayerReport(null), _ => Checked(pinned == null));
            foreach (var path in _context.ArchivedPlayerReports)
                menu.AppendAction("Archived/" + Path.GetFileNameWithoutExtension(path), _ => _context.SelectPlayerReport(path), _ => Checked(path == pinned));
            menu.AppendSeparator();
            menu.AppendAction("Open Report File...", _ => OpenPlayerReportFile());
        }

        private void RebuildLayoutMenu()
        {
            if (_context.LayoutProvider == null)
                return;

            var files = _context.LayoutFiles;
            var pinned = _context.PinnedLayout;
            var current = files.FirstOrDefault(file => file.Path == (pinned ?? files.FirstOrDefault()?.Path));
            _layoutMenu.text = _context.Layout == null
                ? "Addressables: none"
                : $"Addressables: {(pinned == null ? "Latest" : current?.Label ?? Path.GetFileNameWithoutExtension(pinned))}";

            var menu = _layoutMenu.menu;
            menu.ClearItems();
            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var isLatest = i == 0;
                var label = isLatest ? $"{file.Label} (latest)" : file.Label;
                if (i >= RecentLayoutCount)
                    label = "Older/" + label;
                menu.AppendAction(label, _ => _context.SelectLayout(isLatest ? null : file.Path), _ => Checked(pinned == null ? isLatest : file.Path == pinned));
            }

            if (files.Count > 0)
                menu.AppendSeparator();
            var enabled = _context.LayoutProvider.IsLayoutGenerationEnabled;
            menu.AppendAction("Generate Build Layout On Addressables Build", _ => _context.SetLayoutGeneration(!enabled), _ => Checked(enabled));
        }

        private void OpenPlayerReportFile()
        {
            var directory = Directory.Exists(PlayerReportArchive.Folder) ? PlayerReportArchive.Folder : "Library";
            var path = EditorUtility.OpenFilePanel("Open Build Report", directory, "buildreport");
            if (!string.IsNullOrEmpty(path))
                _context.SelectPlayerReport(path);
        }

        private void ShowSnapshotPopup()
        {
            UnityEditor.PopupWindow.Show(_snapshotButton.worldBound, new SnapshotLabelPopup(_context.DefaultSnapshotLabel, label =>
            {
                _context.SaveSnapshot(label);
                ShowNotification(new GUIContent("Snapshot saved"));
            }));
        }

        private void ExportCsv()
        {
            var tab = _tabs[_selectedTab];
            var table = tab.ExportCsv();
            if (table == null)
                return;

            var fileName = $"build-analyzer-{tab.Title.ToLowerInvariant().Replace(' ', '-')}-{DateTime.Now:yyyyMMdd-HHmm}.csv";
            var path = EditorUtility.SaveFilePanel("Export CSV", BuildAnalyzerSettings.instance.ReportsRoot, fileName, "csv");
            if (string.IsNullOrEmpty(path))
                return;

            CsvExporter.Save(table, path);
            EditorUtility.RevealInFinder(path);
        }

        private static DropdownMenuAction.Status Checked(bool isChecked) =>
            isChecked ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;
    }
}
