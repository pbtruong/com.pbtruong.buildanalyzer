using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class CompareTab : IAnalyzerTab
    {
        private const string CurrentKey = "current";
        private static readonly string[] StatusChoices = { "Changed only", "All", "Added", "Removed", "Grew", "Shrank" };
        private static readonly DiffStatus[] StatusByChoice = { default, default, DiffStatus.Added, DiffStatus.Removed, DiffStatus.Grew, DiffStatus.Shrank };

        private sealed class CompareSource
        {
            public CompareSource(string key, string label, Func<BuildSnapshot> load)
            {
                Key = key;
                Label = label;
                Load = load;
            }

            public string Key { get; }
            public string Label { get; }
            public Func<BuildSnapshot> Load { get; }
            public bool IsSnapshot { get; set; }
            public bool IsLayout { get; set; }
        }

        private readonly AnalyzerContext _context;
        private readonly Dictionary<string, BuildSnapshot> _cache = new();
        private readonly VisualElement _root = Ui.Container("ba-page");
        private readonly VisualElement _body = Ui.Container("ba-body");
        private readonly VisualElement _cards = Ui.Container("ba-cards");
        private readonly DropdownField _baseline;
        private readonly DropdownField _target;
        private readonly DropdownField _status;
        private readonly DropdownField _source;
        private readonly Label _summary = Ui.Label(string.Empty, "ba-summary");
        private readonly SizeListView<CategoryDelta> _categories;
        private readonly SizeListView<SizeDiffEntry> _table;
        private List<CompareSource> _sources = new();
        private SnapshotDiff _diff;
        private string _baselineKey;
        private string _targetKey;
        private string _search = string.Empty;
        private bool _suppressEvents;

        public CompareTab(AnalyzerContext context)
        {
            _context = context;
            _context.SnapshotsChanged += () => _cache.Clear();

            var pickers = Ui.Container("ba-filterbar");
            _baseline = Ui.Dropdown("Baseline", new List<string>(), _ => OnSourceChanged());
            _target = Ui.Dropdown("Target", new List<string>(), _ => OnSourceChanged());
            _baseline.AddToClassList("ba-dropdown--wide");
            _target.AddToClassList("ba-dropdown--wide");
            pickers.Add(_baseline);
            pickers.Add(Ui.Button("Swap", Swap, "Swap baseline and target"));
            pickers.Add(_target);
            pickers.Add(Ui.Button("Snapshots Folder", OpenSnapshotsFolder));

            var filters = Ui.Container("ba-filterbar");
            filters.Add(Ui.Search(value =>
            {
                _search = value;
                ApplyFilter();
            }));
            _status = Ui.Dropdown(null, StatusChoices.ToList(), _ => ApplyFilter());
            _source = Ui.Dropdown(null, Ui.WithAll("All sources", Enum.GetNames(typeof(SizeSource))), _ => ApplyFilter());
            filters.Add(_status);
            filters.Add(_source);
            filters.Add(_summary);

            _categories = new SizeListView<CategoryDelta>(new[]
            {
                SizeColumns.Text<CategoryDelta>("Category", delta => delta.Category.DisplayName(), 140, true),
                SizeColumns.Bytes<CategoryDelta>("Baseline", delta => delta.Baseline, 100),
                SizeColumns.Bytes<CategoryDelta>("Target", delta => delta.Target, 100),
                DeltaColumn<CategoryDelta>(delta => delta.Delta)
            }, 3, SortDirection.Descending);
            _categories.AddToClassList("ba-table--compact");

            _table = new SizeListView<SizeDiffEntry>(new[]
            {
                new SizeColumn<SizeDiffEntry>("Status", 75, entry => entry.Status.ToString(), (a, b) => a.Status.CompareTo(b.Status))
                {
                    LabelClass = entry => "ba-status--" + entry.Status.ToString().ToLowerInvariant()
                },
                SizeColumns.AssetName<SizeDiffEntry>(entry => entry.Name, entry => entry.AssetPath),
                SizeColumns.Text<SizeDiffEntry>("Source", entry => entry.Source.ToString(), 90),
                SizeColumns.Text<SizeDiffEntry>("Category", entry => entry.Category.DisplayName(), 95),
                SizeColumns.Bytes<SizeDiffEntry>("Baseline", entry => entry.BaselineSize),
                SizeColumns.Bytes<SizeDiffEntry>("Target", entry => entry.TargetSize),
                DeltaColumn<SizeDiffEntry>(entry => entry.Delta),
                SizeColumns.Text<SizeDiffEntry>("Path", entry => entry.AssetPath, 300, true)
            }, 6, SortDirection.Descending);
            _table.Selected += entry => AssetActions.Ping(entry.AssetPath);
            _table.Chosen += entry => AssetActions.Select(entry.AssetPath);
            _table.ContextMenu = (entry, menu) => AssetActions.AppendMenu(menu, entry.AssetPath);

            _body.Add(pickers);
            _body.Add(_cards);
            _body.Add(Ui.SectionTitle("By category"));
            _body.Add(_categories);
            _body.Add(Ui.SectionTitle("Assets"));
            _body.Add(filters);
            _body.Add(_table);
        }

        public string Title => "Compare";
        public VisualElement Root => _root;
        public bool CanExport => _diff != null;
        public CsvTable ExportCsv() => _diff == null ? null : _table.ToCsv();

        public void Refresh()
        {
            _root.Clear();
            _sources = BuildSources();
            if (_sources.Count(source => source.Key != CurrentKey) == 0 && !_context.HasData)
            {
                _root.Add(Ui.Empty("Nothing to compare yet. Save a snapshot after each build (toolbar > Save Snapshot), or build Addressables twice with build layout enabled."));
                return;
            }

            _root.Add(_body);
            ApplySourceChoices();
            Compare();
        }

        private List<CompareSource> BuildSources()
        {
            var sources = new List<CompareSource>();
            if (_context.HasData)
                sources.Add(new CompareSource(CurrentKey, "Current (loaded data)", () => _context.CreateSnapshot("Current")));

            foreach (var snapshot in SnapshotStore.List())
            {
                sources.Add(new CompareSource(snapshot.Path, $"Snapshot · {snapshot.CreatedAt:yyyy-MM-dd HH:mm} · {snapshot.Label}",
                    () => Cached(snapshot.Path, () => SnapshotStore.Load(snapshot.Path))) { IsSnapshot = true });
            }

            foreach (var layout in _context.LayoutFiles)
            {
                sources.Add(new CompareSource(layout.Path, $"Addressables layout · {layout.Label}",
                    () => Cached(layout.Path, () => SnapshotBuilder.Build(layout.Label, null, _context.LoadLayoutFile(layout.Path)))) { IsLayout = true });
            }

            return sources;
        }

        private void ApplySourceChoices()
        {
            var labels = _sources.Select(source => source.Label).ToList();
            var baselineIndex = _sources.FindIndex(source => source.Key == _baselineKey);
            var targetIndex = _sources.FindIndex(source => source.Key == _targetKey);
            if (baselineIndex < 0 || targetIndex < 0)
                (baselineIndex, targetIndex) = DefaultPair();

            _suppressEvents = true;
            _baseline.choices = labels;
            _target.choices = labels;
            _baseline.index = baselineIndex;
            _target.index = targetIndex;
            _suppressEvents = false;
            _baselineKey = _sources[baselineIndex].Key;
            _targetKey = _sources[targetIndex].Key;
        }

        private (int baseline, int target) DefaultPair()
        {
            var snapshots = IndicesWhere(source => source.IsSnapshot);
            var layouts = IndicesWhere(source => source.IsLayout);
            if (snapshots.Count >= 2)
                return (snapshots[1], snapshots[0]);
            if (layouts.Count >= 2)
                return (layouts[layouts.Count - 1], layouts[0]);
            if (snapshots.Count == 1)
                return (snapshots[0], 0);
            return (0, 0);
        }

        private List<int> IndicesWhere(Func<CompareSource, bool> predicate) =>
            _sources.Select((source, index) => (source, index)).Where(pair => predicate(pair.source)).Select(pair => pair.index).ToList();

        private void OnSourceChanged()
        {
            if (_suppressEvents || _baseline.index < 0 || _target.index < 0)
                return;

            _baselineKey = _sources[_baseline.index].Key;
            _targetKey = _sources[_target.index].Key;
            Compare();
        }

        private void Swap()
        {
            (_baselineKey, _targetKey) = (_targetKey, _baselineKey);
            ApplySourceChoices();
            Compare();
        }

        private void Compare()
        {
            var baselineSource = _sources.FirstOrDefault(source => source.Key == _baselineKey);
            var targetSource = _sources.FirstOrDefault(source => source.Key == _targetKey);
            if (baselineSource == null || targetSource == null)
                return;

            BuildSnapshot baseline;
            BuildSnapshot target;
            try
            {
                EditorUtility.DisplayProgressBar("Build Analyzer", "Loading comparison data...", 0.5f);
                baseline = baselineSource.Load();
                target = targetSource.Load();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            _diff = SnapshotComparer.Compare(baseline, target);
            RenderCards(baseline, target);
            _categories.SetItems(_diff.Categories);
            ApplyFilter();
        }

        private void RenderCards(BuildSnapshot baseline, BuildSnapshot target)
        {
            _cards.Clear();
            _cards.Add(Ui.Card("Baseline", SizeFormat.Bytes(_diff.BaselineTotal), $"{baseline.Entries.Count} assets"));
            _cards.Add(Ui.Card("Target", SizeFormat.Bytes(_diff.TargetTotal), $"{target.Entries.Count} assets"));
            _cards.Add(Ui.Card("Delta", SizeFormat.Signed(_diff.Delta), SizeFormat.Percent(_diff.Delta, _diff.BaselineTotal), Ui.DeltaClass(_diff.Delta)));
            AddStatusCard(DiffStatus.Added);
            AddStatusCard(DiffStatus.Removed);
            AddStatusCard(DiffStatus.Grew);
            AddStatusCard(DiffStatus.Shrank);

            if (baseline.PlayerOutputSize > 0 && target.PlayerOutputSize > 0)
            {
                var delta = target.PlayerOutputSize - baseline.PlayerOutputSize;
                _cards.Add(Ui.Card("Player Output", SizeFormat.Signed(delta), $"{SizeFormat.Bytes(baseline.PlayerOutputSize)} → {SizeFormat.Bytes(target.PlayerOutputSize)}", Ui.DeltaClass(delta)));
            }

            if (baseline.BundlesCompressedSize > 0 && target.BundlesCompressedSize > 0)
            {
                var delta = target.BundlesCompressedSize - baseline.BundlesCompressedSize;
                _cards.Add(Ui.Card("Bundles On Disk", SizeFormat.Signed(delta), $"{SizeFormat.Bytes(baseline.BundlesCompressedSize)} → {SizeFormat.Bytes(target.BundlesCompressedSize)}", Ui.DeltaClass(delta)));
            }
        }

        private void AddStatusCard(DiffStatus status) =>
            _cards.Add(Ui.Card(status.ToString(), _diff.Count(status).ToString(), SizeFormat.Signed(_diff.DeltaOf(status))));

        private void ApplyFilter()
        {
            if (_diff == null)
                return;

            var filtered = _diff.Entries.Where(MatchesStatus)
                .Where(entry => _source.index <= 0 || entry.Source == (SizeSource)(_source.index - 1))
                .Where(entry => Ui.Matches(_search, entry.AssetPath))
                .ToList();
            _table.SetItems(filtered);
            _summary.text = $"{filtered.Count} assets · net {SizeFormat.Signed(filtered.Sum(entry => entry.Delta))}";
        }

        private bool MatchesStatus(SizeDiffEntry entry) => _status.index switch
        {
            <= 0 => entry.Status != DiffStatus.Unchanged,
            1 => true,
            _ => entry.Status == StatusByChoice[_status.index]
        };

        private BuildSnapshot Cached(string key, Func<BuildSnapshot> load)
        {
            if (!_cache.TryGetValue(key, out var snapshot))
            {
                snapshot = load();
                _cache[key] = snapshot;
            }

            return snapshot;
        }

        private static void OpenSnapshotsFolder()
        {
            Directory.CreateDirectory(SnapshotStore.Folder);
            EditorUtility.RevealInFinder(SnapshotStore.Folder);
        }

        private static SizeColumn<T> DeltaColumn<T>(Func<T, long> delta) =>
            new("Delta", 95, item => SizeFormat.Signed(delta(item)), (a, b) => Math.Abs(delta(a)).CompareTo(Math.Abs(delta(b))))
            {
                Numeric = true,
                LabelClass = item => Ui.DeltaClass(delta(item)),
                CsvValue = item => SizeFormat.Raw(delta(item))
            };
    }
}
