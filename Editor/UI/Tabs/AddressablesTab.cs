using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class AddressablesTab : IAnalyzerTab
    {
        private enum Mode
        {
            Bundles,
            Assets,
            Duplicates
        }

        private enum NodeKind
        {
            Group,
            Bundle,
            Asset
        }

        private sealed class LayoutNode
        {
            public LayoutNode(NodeKind kind, string name, string assetPath, long compressed, long uncompressed, int assetCount, string info, bool isImplicit)
            {
                Kind = kind;
                Name = name;
                AssetPath = assetPath;
                Compressed = compressed;
                Uncompressed = uncompressed;
                AssetCount = assetCount;
                Info = info;
                IsImplicit = isImplicit;
            }

            public NodeKind Kind { get; }
            public string Name { get; }
            public string AssetPath { get; }
            public long Compressed { get; }
            public long Uncompressed { get; }
            public int AssetCount { get; }
            public string Info { get; }
            public bool IsImplicit { get; }
        }

        private readonly AnalyzerContext _context;
        private readonly VisualElement _root = Ui.Container("ba-page");
        private readonly VisualElement _body = Ui.Container("ba-body");
        private readonly VisualElement _tableHost = Ui.Container("ba-body");
        private readonly Label _header = Ui.Label(string.Empty, "ba-summary");
        private readonly Label _summary = Ui.Label(string.Empty, "ba-summary");
        private readonly Label _details = Ui.Label("Select a row to see details. Implicit assets (italic) are pulled into a bundle by dependencies, not marked Addressable.", "ba-details");
        private readonly List<Button> _modeButtons = new();
        private readonly ToolbarToggle _implicitOnly;
        private readonly SizeTreeView<LayoutNode> _tree;
        private readonly SizeListView<SizeEntry> _assets;
        private readonly SizeListView<DuplicateAssetInfo> _duplicates;
        private Mode _mode;
        private string _search = string.Empty;

        public AddressablesTab(AnalyzerContext context)
        {
            _context = context;

            var modes = Ui.Container("ba-modes");
            foreach (Mode mode in Enum.GetValues(typeof(Mode)))
            {
                var button = new Button(() => SetMode(mode)) { text = mode.ToString() };
                button.AddToClassList("ba-mode");
                _modeButtons.Add(button);
                modes.Add(button);
            }

            var filters = Ui.Container("ba-filterbar");
            filters.Add(modes);
            filters.Add(Ui.Search(value =>
            {
                _search = value;
                ApplyFilter();
            }));
            _implicitOnly = new ToolbarToggle { text = "Implicit only", tooltip = "Only assets pulled in by dependencies" };
            _implicitOnly.AddToClassList("ba-toggle");
            _implicitOnly.RegisterValueChangedCallback(_ => ApplyFilter());
            filters.Add(_implicitOnly);
            filters.Add(_summary);

            _tree = new SizeTreeView<LayoutNode>(new[]
            {
                new SizeColumn<LayoutNode>("Name", 300, node => node.Name, (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase))
                {
                    Stretch = true,
                    Icon = NodeIcon,
                    LabelClass = node => node.IsImplicit ? "ba-implicit" : null
                },
                new SizeColumn<LayoutNode>("On Disk", 85, node => node.Kind == NodeKind.Asset ? string.Empty : SizeFormat.Bytes(node.Compressed), (a, b) => a.Compressed.CompareTo(b.Compressed)) { Numeric = true },
                SizeColumns.Bytes<LayoutNode>("Uncompressed", node => node.Uncompressed, 95),
                new SizeColumn<LayoutNode>("Assets", 60, node => node.Kind == NodeKind.Asset ? string.Empty : node.AssetCount.ToString(), (a, b) => a.AssetCount.CompareTo(b.AssetCount)) { Numeric = true },
                SizeColumns.Text<LayoutNode>("Info", node => node.Info, 240, true)
            }, 1, SortDirection.Descending);
            _tree.Selected += ShowNodeDetails;
            _tree.Chosen += node => AssetActions.Select(node.AssetPath);
            _tree.ContextMenu = (node, menu) =>
            {
                if (node.Kind == NodeKind.Asset)
                    AssetActions.AppendMenu(menu, node.AssetPath);
                else
                    menu.AppendAction("Copy Name", _ => GUIUtility.systemCopyBuffer = node.Name);
            };

            _assets = new SizeListView<SizeEntry>(new[]
            {
                SizeColumns.AssetName<SizeEntry>(entry => entry.Name, entry => entry.AssetPath),
                SizeColumns.Bytes<SizeEntry>("Size", entry => entry.Size),
                SizeColumns.Text<SizeEntry>("Category", entry => entry.Category.DisplayName(), 95),
                SizeColumns.Text<SizeEntry>("Type", entry => entry.TypeName, 110),
                SizeColumns.Text<SizeEntry>("Kind", entry => entry.IsImplicit ? "Implicit" : "Addressable", 85),
                new SizeColumn<SizeEntry>("Bundles", 60, entry => entry.Containers.Length.ToString(), (a, b) => a.Containers.Length.CompareTo(b.Containers.Length)) { Numeric = true },
                SizeColumns.Text<SizeEntry>("In Bundles", entry => entry.Container, 300, true)
            }, 1, SortDirection.Descending);
            _assets.Selected += entry =>
            {
                AssetActions.Ping(entry.AssetPath);
                _details.text = $"{entry.AssetPath}\n{(entry.IsImplicit ? "Implicit" : "Addressable")} · in {entry.Container}";
            };
            _assets.Chosen += entry => AssetActions.Select(entry.AssetPath);
            _assets.ContextMenu = (entry, menu) => AssetActions.AppendMenu(menu, entry.AssetPath);

            _duplicates = new SizeListView<DuplicateAssetInfo>(new[]
            {
                SizeColumns.AssetName<DuplicateAssetInfo>(duplicate => duplicate.Name, duplicate => duplicate.AssetPath),
                SizeColumns.Bytes<DuplicateAssetInfo>("Wasted", duplicate => duplicate.WastedSize),
                SizeColumns.Bytes<DuplicateAssetInfo>("Per Copy", duplicate => duplicate.SizePerCopy),
                SizeColumns.Number<DuplicateAssetInfo>("Copies", duplicate => duplicate.Bundles.Count),
                SizeColumns.Text<DuplicateAssetInfo>("Category", duplicate => duplicate.Category.DisplayName(), 95),
                SizeColumns.Text<DuplicateAssetInfo>("Bundles", duplicate => string.Join(", ", duplicate.Bundles), 320, true)
            }, 1, SortDirection.Descending);
            _duplicates.Selected += duplicate =>
            {
                AssetActions.Ping(duplicate.AssetPath);
                _details.text = $"{duplicate.AssetPath}\nCopied into: {string.Join(", ", duplicate.Bundles)}";
            };
            _duplicates.Chosen += duplicate => AssetActions.Select(duplicate.AssetPath);
            _duplicates.ContextMenu = (duplicate, menu) => AssetActions.AppendMenu(menu, duplicate.AssetPath);

            _body.Add(_header);
            _body.Add(filters);
            _body.Add(_tableHost);
            _body.Add(_details);
            SetMode(Mode.Bundles);
        }

        public string Title => "Addressables";
        public VisualElement Root => _root;
        public bool CanExport => _context.Layout != null;

        public CsvTable ExportCsv()
        {
            if (_context.Layout == null)
                return null;

            return _mode switch
            {
                Mode.Assets => _assets.ToCsv(),
                Mode.Duplicates => _duplicates.ToCsv(),
                _ => new CsvTable(
                    new[] { "Group", "Bundle", "Compression", "Bundle On Disk", "Asset", "Kind", "Category", "Type", "Asset Size" },
                    (from grp in _context.Layout.Groups
                        from bundle in grp.Bundles
                        from asset in bundle.Assets
                        select new[]
                        {
                            grp.Name, bundle.Name, bundle.Compression, SizeFormat.Raw(bundle.CompressedSize), asset.AssetPath,
                            asset.IsImplicit ? "Implicit" : "Addressable", asset.Category.DisplayName(), asset.TypeName, SizeFormat.Raw(asset.Size)
                        }).ToList())
            };
        }

        public void Refresh()
        {
            _root.Clear();
            if (_context.LayoutProvider == null)
            {
                _root.Add(Ui.Empty("The Addressables package (1.21+) is not installed in this project."));
                return;
            }

            var layout = _context.Layout;
            if (layout == null)
            {
                _root.Add(CreateMissingLayoutState());
                return;
            }

            _header.text = $"Layout {layout.BuildStart:yyyy-MM-dd HH:mm} · {layout.Platform} · {layout.Groups.Count} groups · {layout.Bundles.Count} bundles · " +
                           $"{SizeFormat.Bytes(layout.TotalCompressedSize)} on disk · {SizeFormat.Bytes(layout.TotalUncompressedSize)} uncompressed";
            _root.Add(_body);
            ApplyFilter();
        }

        private VisualElement CreateMissingLayoutState()
        {
            var container = Ui.Container("ba-empty-state");
            var enabled = _context.LayoutProvider.IsLayoutGenerationEnabled;
            container.Add(Ui.Empty(enabled
                ? "No Addressables build layout found. Build Addressables content (Addressables Groups > Build > New Build), then press Reload."
                : "Build layout generation is disabled. Enable it, then build Addressables content and press Reload.", HelpBoxMessageType.Warning));
            if (!enabled)
                container.Add(Ui.Button("Enable Build Layout", () => _context.SetLayoutGeneration(true)));
            return container;
        }

        private void SetMode(Mode mode)
        {
            _mode = mode;
            for (var i = 0; i < _modeButtons.Count; i++)
                _modeButtons[i].EnableInClassList("ba-mode--selected", i == (int)mode);

            _implicitOnly.SetEnabled(mode != Mode.Duplicates);
            _tableHost.Clear();
            _tableHost.Add(mode switch
            {
                Mode.Assets => (VisualElement)_assets,
                Mode.Duplicates => _duplicates,
                _ => _tree
            });
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var layout = _context.Layout;
            if (layout == null)
                return;

            switch (_mode)
            {
                case Mode.Bundles:
                    var roots = BuildTree(layout);
                    _tree.SetRoots(roots, _search.Length > 0);
                    _summary.text = $"{roots.Sum(group => group.Children.Count)} bundles shown";
                    break;
                case Mode.Assets:
                    var assets = layout.Assets.Where(entry => (!_implicitOnly.value || entry.IsImplicit) && Ui.Matches(_search, entry.AssetPath, entry.TypeName, entry.Container)).ToList();
                    _assets.SetItems(assets);
                    _summary.text = $"{assets.Count} / {layout.Assets.Count} assets · {SizeFormat.Bytes(assets.Sum(entry => entry.Size))}";
                    break;
                case Mode.Duplicates:
                    var duplicates = layout.Duplicates.Where(duplicate => Ui.Matches(_search, duplicate.AssetPath, string.Join(" ", duplicate.Bundles))).ToList();
                    _duplicates.SetItems(duplicates);
                    _summary.text = $"{duplicates.Count} duplicated assets · {SizeFormat.Bytes(duplicates.Sum(duplicate => duplicate.WastedSize))} wasted";
                    break;
            }
        }

        private IReadOnlyList<TreeNode<LayoutNode>> BuildTree(BundleLayoutData layout)
        {
            var implicitOnly = _implicitOnly.value;
            var filtering = implicitOnly || _search.Length > 0;
            var groups = new List<TreeNode<LayoutNode>>();

            foreach (var group in layout.Groups)
            {
                var bundles = new List<TreeNode<LayoutNode>>();
                foreach (var bundle in group.Bundles)
                {
                    var bundleMatches = _search.Length > 0 && Ui.Matches(_search, bundle.Name);
                    var assets = bundle.Assets
                        .Where(asset => (!implicitOnly || asset.IsImplicit) && (bundleMatches || Ui.Matches(_search, asset.AssetPath, asset.Address, asset.TypeName)))
                        .Select(asset => new TreeNode<LayoutNode>(AssetNode(asset)))
                        .ToList();
                    if (filtering && assets.Count == 0 && !(bundleMatches && !implicitOnly))
                        continue;
                    bundles.Add(new TreeNode<LayoutNode>(BundleNode(bundle), assets));
                }

                if (filtering && bundles.Count == 0)
                    continue;
                groups.Add(new TreeNode<LayoutNode>(
                    new LayoutNode(NodeKind.Group, group.Name, null, group.CompressedSize, group.UncompressedSize, group.AssetCount, group.Bundles.Count == 1 ? "1 bundle" : $"{group.Bundles.Count} bundles", false),
                    bundles));
            }

            return groups;
        }

        private static LayoutNode BundleNode(BundleInfo bundle) =>
            new(NodeKind.Bundle, bundle.Name, null, bundle.CompressedSize, bundle.UncompressedSize, bundle.Assets.Count,
                $"{bundle.Compression} · {bundle.LoadPath}", false);

        private static LayoutNode AssetNode(BundleAssetInfo asset) =>
            new(NodeKind.Asset, asset.Name, asset.AssetPath, asset.Size, asset.Size, 0,
                asset.IsImplicit ? $"Implicit · {asset.Category.DisplayName()}" : $"{asset.Category.DisplayName()} · {asset.Address}",
                asset.IsImplicit);

        private static Texture NodeIcon(LayoutNode node) => node.Kind switch
        {
            NodeKind.Group => AssetIcons.Builtin("Folder Icon"),
            NodeKind.Bundle => AssetIcons.Builtin("DefaultAsset Icon"),
            _ => AssetIcons.For(node.AssetPath)
        };

        private void ShowNodeDetails(LayoutNode node)
        {
            if (node.Kind == NodeKind.Asset)
                AssetActions.Ping(node.AssetPath);

            _details.text = node.Kind switch
            {
                NodeKind.Asset => $"{node.AssetPath}\n{node.Info} · {SizeFormat.Bytes(node.Uncompressed)}",
                NodeKind.Bundle => $"{node.Name}\n{node.Info} · {SizeFormat.Bytes(node.Compressed)} on disk · {SizeFormat.Bytes(node.Uncompressed)} uncompressed",
                _ => $"Group {node.Name} · {node.Info} · {SizeFormat.Bytes(node.Compressed)} on disk"
            };
        }
    }
}
