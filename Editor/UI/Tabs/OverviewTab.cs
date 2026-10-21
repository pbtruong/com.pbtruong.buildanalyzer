using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.UIElements;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class OverviewTab : IAnalyzerTab
    {
        private const int TopAssetCount = 25;
        private const string PlayerFill = "ba-fill--player";
        private const string BundlesFill = "ba-fill--bundles";

        private readonly AnalyzerContext _context;
        private readonly ScrollView _root = new();

        public OverviewTab(AnalyzerContext context)
        {
            _context = context;
            _root.AddToClassList("ba-page");
            _root.AddToClassList("ba-page--scroll");
        }

        public string Title => "Overview";
        public VisualElement Root => _root;
        public bool CanExport => false;
        public CsvTable ExportCsv() => null;

        public void Refresh()
        {
            _root.Clear();
            if (!_context.HasData)
            {
                _root.Add(Ui.Empty("No build data found.\n\n" +
                                   "• Player: make a player build (File > Build Profiles), then press Reload.\n" +
                                   "• Addressables: enable 'Generate Build Layout' in the Addressables menu of the toolbar, then build Addressables content."));
                return;
            }

            _root.Add(CreateCards());
            if (_context.Player != null)
                AddShippedContent(_context.Player);

            _root.Add(Ui.SectionTitle("Size by category"));
            _root.Add(Ui.Legend(("Player build", PlayerFill), ("Addressables", BundlesFill)));
            _root.Add(new BarChart(CategoryRows()));
            _root.Add(Ui.Label("Asset sizes are the uncompressed data written into the build. On-disk size is smaller once bundles/APK are compressed.", "ba-note"));

            _root.Add(Ui.SectionTitle($"Largest {TopAssetCount} assets"));
            _root.Add(CreateTopAssets());

            if (_context.Player?.IncludedModules.Count > 0)
                _root.Add(CreateModulesFoldout());
        }

        private VisualElement CreateCards()
        {
            var player = _context.Player;
            var layout = _context.Layout;
            var cards = Ui.Container("ba-cards");

            cards.Add(Ui.Card("Platform", player != null ? player.Platform.ToString() : layout.Platform));
            if (player != null)
            {
                cards.Add(Ui.Card(player.HasOutputFile ? Path.GetExtension(player.OutputPath).TrimStart('.').ToUpperInvariant() + " On Disk" : "Player Output",
                    SizeFormat.Bytes(player.OutputSize),
                    player.HasOutputFile ? Path.GetFileName(player.OutputPath) : $"report total, {player.Files.Count} files"));
                cards.Add(Ui.Card("Build", player.Result.ToString(), BuildHint(player),
                    player.IsDevelopment ? "ba-text--warning" : null));
                cards.Add(Ui.Card("Player Assets", SizeFormat.Bytes(player.AssetsSize), $"{player.Assets.Count} assets"));
            }

            if (layout != null)
            {
                cards.Add(Ui.Card("Addressables On Disk", SizeFormat.Bytes(layout.TotalCompressedSize),
                    $"{layout.Bundles.Count} bundles · {SizeFormat.Bytes(layout.TotalUncompressedSize)} uncompressed"));
                cards.Add(Ui.Card("Addressables Assets", SizeFormat.Bytes(layout.Assets.Sum(entry => entry.Size)),
                    $"{layout.Assets.Count} assets · built {layout.BuildStart:yyyy-MM-dd HH:mm}"));
                cards.Add(Ui.Card("Duplicated In Bundles", SizeFormat.Bytes(layout.DuplicatedWaste),
                    $"{layout.Duplicates.Count} assets copied into several bundles",
                    layout.DuplicatedWaste > 0 ? "ba-text--warning" : null));
            }

            var crossDuplicates = PlayerBundleDuplicates.Find(player, layout);
            if (crossDuplicates.Count > 0)
            {
                cards.Add(Ui.Card("In Player + Bundles", SizeFormat.Bytes(crossDuplicates.Sum(duplicate => duplicate.WastedSize)),
                    $"{crossDuplicates.Count} assets shipped twice", "ba-text--warning"));
            }

            return cards;
        }

        private static string BuildHint(PlayerBuildData player)
        {
            var parts = new List<string> { player.BuildStartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm") };
            if (player.TotalTime < TimeSpan.FromDays(1))
                parts.Add(player.TotalTime.ToString(@"hh\:mm\:ss"));
            if (player.IsDevelopment)
                parts.Add("Development build");
            return string.Join(" · ", parts);
        }

        private IReadOnlyList<BarRow> CategoryRows()
        {
            var playerSizes = SumByCategory(_context.Player?.Assets);
            var bundleSizes = SumByCategory(_context.Layout?.Assets);

            return Enum.GetValues(typeof(AssetCategory)).Cast<AssetCategory>()
                .Select(category => (category, player: playerSizes.GetValueOrDefault(category), bundles: bundleSizes.GetValueOrDefault(category)))
                .Where(row => row.player + row.bundles > 0)
                .OrderByDescending(row => row.player + row.bundles)
                .Select(row => new BarRow(
                    row.category.DisplayName(),
                    new[] { new BarSegment(row.player, PlayerFill), new BarSegment(row.bundles, BundlesFill) },
                    SizeFormat.Bytes(row.player + row.bundles),
                    $"Player {SizeFormat.Bytes(row.player)} · Addressables {SizeFormat.Bytes(row.bundles)}"))
                .ToList();
        }

        private void AddShippedContent(PlayerBuildData player)
        {
            var breakdown = PlayerOutputGroups.Summarize(player);
            if (breakdown.Shipped.Count == 0)
                return;

            _root.Add(Ui.SectionTitle("What goes into the build"));
            _root.Add(new BarChart(breakdown.Shipped
                .Select(group => new BarRow(group.Name, new[] { new BarSegment(group.Size, PlayerFill) }, SizeFormat.Bytes(group.Size), $"{group.FileCount} files"))
                .ToList()));
            _root.Add(Ui.Label(
                $"Files staged for packaging, before APK/AAB compression. Hidden: {SizeFormat.Bytes(breakdown.IntermediateSize)} of intermediate files " +
                $"({breakdown.IntermediateCount} IL2CPP sources, symbols, managed DLLs) that are not shipped.", "ba-note"));
        }

        private VisualElement CreateTopAssets()
        {
            var list = Ui.Container("ba-top");
            var entries = (_context.Player?.Assets ?? Array.Empty<SizeEntry>())
                .Concat(_context.Layout?.Assets ?? Array.Empty<SizeEntry>())
                .OrderByDescending(entry => entry.Size)
                .Take(TopAssetCount);

            foreach (var entry in entries)
                list.Add(CreateTopRow(entry));
            return list;
        }

        private static VisualElement CreateTopRow(SizeEntry entry)
        {
            var row = Ui.Container("ba-top-row");
            row.tooltip = $"{entry.AssetPath}\n{entry.Container}\nClick: ping · Double-click: select";

            var icon = new Image { image = AssetIcons.For(entry.AssetPath) };
            icon.AddToClassList("ba-cell__icon");
            row.Add(icon);
            row.Add(Ui.Label(entry.Name, "ba-top-row__name"));
            row.Add(Ui.Label(entry.AssetPath, "ba-top-row__path"));

            var isPlayer = entry.Source == SizeSource.Player;
            var copies = entry.Containers.Length;
            var badge = Ui.Label(isPlayer ? "Player" : copies > 1 ? $"Addressables ×{copies}" : "Addressables", "ba-badge");
            badge.AddToClassList(isPlayer ? "ba-badge--player" : "ba-badge--bundles");
            row.Add(badge);
            row.Add(Ui.Label(entry.Category.DisplayName(), "ba-top-row__category"));
            row.Add(Ui.Label(SizeFormat.Bytes(entry.Size), "ba-top-row__size"));

            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.clickCount >= 2)
                    AssetActions.Select(entry.AssetPath);
                else
                    AssetActions.Ping(entry.AssetPath);
            });
            return row;
        }

        private VisualElement CreateModulesFoldout()
        {
            var modules = _context.Player.IncludedModules;
            var foldout = new Foldout { text = $"Engine modules included ({modules.Count})", value = false };
            foldout.AddToClassList("ba-foldout");
            foldout.Add(Ui.Label(string.Join(", ", modules), "ba-wrap"));
            return foldout;
        }

        private static Dictionary<AssetCategory, long> SumByCategory(IEnumerable<SizeEntry> entries) =>
            entries == null
                ? new Dictionary<AssetCategory, long>()
                : entries.GroupBy(entry => entry.Category).ToDictionary(group => group.Key, group => group.Sum(entry => entry.Size));
    }
}
