using System;
using System.IO;
using System.Linq;
using UnityEngine.UIElements;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class PlayerAssetsTab : IAnalyzerTab
    {
        private readonly AnalyzerContext _context;
        private readonly VisualElement _root = Ui.Container("ba-page");
        private readonly VisualElement _body = Ui.Container("ba-body");
        private readonly DropdownField _category;
        private readonly DropdownField _location;
        private readonly Label _summary = Ui.Label(string.Empty, "ba-summary");
        private readonly Label _details = Ui.Label("Select an asset to see where it is packed.", "ba-details");
        private readonly SizeListView<SizeEntry> _table;
        private string _search = string.Empty;

        public PlayerAssetsTab(AnalyzerContext context)
        {
            _context = context;

            var filters = Ui.Container("ba-filterbar");
            filters.Add(Ui.Search(value =>
            {
                _search = value;
                ApplyFilter();
            }));
            _category = Ui.Dropdown(null, Ui.WithAll("All categories", Enum.GetValues(typeof(AssetCategory)).Cast<AssetCategory>().Select(c => c.DisplayName())), _ => ApplyFilter());
            _location = Ui.Dropdown(null, Ui.WithAll("All locations", Enum.GetNames(typeof(PlayerLocation))), _ => ApplyFilter());
            _location.tooltip = "Resources = resources.assets, Scenes = levelN/sharedassetsN, Global = globalgamemanagers";
            filters.Add(_category);
            filters.Add(_location);
            filters.Add(_summary);

            _table = new SizeListView<SizeEntry>(new[]
            {
                SizeColumns.AssetName<SizeEntry>(entry => entry.Name, entry => entry.AssetPath),
                SizeColumns.Bytes<SizeEntry>("Size", entry => entry.Size),
                new SizeColumn<SizeEntry>("%", 55, entry => SizeFormat.Percent(entry.Size, _context.Player.AssetsSize), (a, b) => a.Size.CompareTo(b.Size)) { Numeric = true },
                SizeColumns.Text<SizeEntry>("Category", entry => entry.Category.DisplayName(), 95),
                SizeColumns.Text<SizeEntry>("Type", entry => entry.TypeName, 110),
                SizeColumns.Text<SizeEntry>("Packed In", entry => entry.Container, 190),
                SizeColumns.Text<SizeEntry>("Path", entry => entry.AssetPath, 320, true)
            }, 1, SortDirection.Descending);
            _table.Selected += ShowDetails;
            _table.Chosen += entry => AssetActions.Select(entry.AssetPath);
            _table.ContextMenu = (entry, menu) => AssetActions.AppendMenu(menu, entry.AssetPath);

            _body.Add(filters);
            _body.Add(_table);
            _body.Add(_details);
        }

        public string Title => "Player Assets";
        public VisualElement Root => _root;
        public bool CanExport => _context.Player != null;
        public CsvTable ExportCsv() => _table.ToCsv();

        public void Refresh()
        {
            _root.Clear();
            if (_context.Player == null)
            {
                _root.Add(Ui.Empty("No player build report found. Make a player build (File > Build Profiles) and press Reload, or open an archived .buildreport from the Player menu."));
                return;
            }

            _root.Add(_body);
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var player = _context.Player;
            if (player == null)
                return;

            var filtered = player.Assets.Where(Matches).ToList();
            _table.SetItems(filtered);
            _summary.text = $"{filtered.Count} / {player.Assets.Count} assets · {SizeFormat.Bytes(filtered.Sum(entry => entry.Size))}";
        }

        private bool Matches(SizeEntry entry) =>
            (_category.index <= 0 || entry.Category == (AssetCategory)(_category.index - 1))
            && (_location.index <= 0 || PlayerLocations.IsIn(entry, (PlayerLocation)(_location.index - 1)))
            && Ui.Matches(_search, entry.AssetPath, entry.TypeName, entry.Container);

        private void ShowDetails(SizeEntry entry)
        {
            AssetActions.Ping(entry.AssetPath);
            var usage = _context.Player.ScenesUsingAsset;
            var scenes = usage.TryGetValue(entry.AssetPath, out var scenePaths)
                ? string.Join(", ", scenePaths.Select(Path.GetFileNameWithoutExtension))
                : usage.Count == 0 ? "n/a (build with the Detailed Build Report option)" : "-";
            _details.text = $"{entry.AssetPath}\nPacked in: {entry.Container}    ·    Used by scenes: {scenes}";
        }
    }
}
