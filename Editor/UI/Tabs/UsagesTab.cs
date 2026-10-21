using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class UsagesTab : IAnalyzerTab
    {
        private const int MaxTreeNodes = 5000;
        private const int MaxTreeDepth = 12;
        private const string ProgressTitle = "Build Analyzer";

        private sealed class UsageNode
        {
            public UsageNode(string path, string referencedPath, string kind, string roots, long buildSize)
            {
                Path = path;
                ReferencedPath = referencedPath;
                Kind = kind;
                Roots = roots;
                BuildSize = buildSize;
            }

            public string Path { get; }
            public string ReferencedPath { get; }
            public string Kind { get; }
            public string Roots { get; }
            public long BuildSize { get; }
            public string Name => AssetPaths.DisplayName(Path);
        }

        private sealed class TreeBudget
        {
            public int Remaining = MaxTreeNodes;
        }

        private readonly AnalyzerContext _context;
        private readonly VisualElement _root = Ui.Container("ba-page");
        private readonly ObjectField _target;
        private readonly Label _status = Ui.Label(string.Empty, "ba-summary");
        private readonly Label _evidence = Ui.Label("Pick an asset, or right-click any row in the other tabs and choose Find Usages In Build.", "ba-details");
        private readonly SizeTreeView<UsageNode> _tree;
        private readonly SizeListView<UsagePath> _paths;
        private readonly SizeListView<ReferenceSite> _sites;
        private readonly Label _pathsTitle = Ui.SectionTitle("Paths from build roots");
        private readonly Label _sitesTitle = Ui.SectionTitle("References inside");
        private readonly VisualElement _sitesHost = Ui.Container("ba-body");
        private string _assetPath;
        private UsageNode _selected;

        public UsagesTab(AnalyzerContext context)
        {
            _context = context;

            var filters = Ui.Container("ba-filterbar");
            _target = new ObjectField { objectType = typeof(Object), allowSceneObjects = false };
            _target.AddToClassList("ba-object-field");
            _target.RegisterValueChangedCallback(evt => Show(evt.newValue != null ? AssetDatabase.GetAssetPath(evt.newValue) : null));
            filters.Add(_target);
            filters.Add(Ui.Button("Use Selection", UseSelection, "Inspect the asset selected in the Project window"));
            filters.Add(Ui.Button("Rebuild Graph", () =>
            {
                RebuildGraph();
                Show(_assetPath);
            }, "Rescan dependencies from scenes in build, Addressables, Resources and preloaded assets (unchanged assets are reused from cache)"));
            filters.Add(_status);

            _tree = new SizeTreeView<UsageNode>(new[]
            {
                SizeColumns.AssetName<UsageNode>(node => node.Name, node => node.Path, 260),
                SizeColumns.Text<UsageNode>("Type", node => node.Kind, 90),
                SizeColumns.Text<UsageNode>("Build Root", node => node.Roots, 180),
                new SizeColumn<UsageNode>("Build Size", 80, node => node.BuildSize < 0 ? "-" : SizeFormat.Bytes(node.BuildSize), (a, b) => a.BuildSize.CompareTo(b.BuildSize)) { Numeric = true }
            }, 0, SortDirection.Ascending);
            _tree.Selected += ShowSites;
            _tree.Chosen += node => AssetActions.Select(node.Path);
            _tree.ContextMenu = (node, menu) => AssetActions.AppendMenu(menu, node.Path);

            _paths = new SizeListView<UsagePath>(new[]
            {
                SizeColumns.Text<UsagePath>("Root", path => path.Root.Label, 150),
                SizeColumns.Number<UsagePath>("Hops", path => path.Chain.Count - 1, 45),
                SizeColumns.Text<UsagePath>("Chain", path => path.ChainText, 400, true)
            }, 1, SortDirection.Ascending);
            _paths.AddToClassList("ba-table--compact");
            _paths.Chosen += path => AssetActions.Select(path.Root.AssetPath);
            _paths.ContextMenu = (path, menu) => AssetActions.AppendMenu(menu, path.Root.AssetPath);

            _sites = new SizeListView<ReferenceSite>(new[]
            {
                SizeColumns.Text<ReferenceSite>("Object", site => site.ObjectPath, 220, true),
                SizeColumns.Text<ReferenceSite>("Component", site => site.ComponentType, 120),
                SizeColumns.Text<ReferenceSite>("Field", site => site.Property, 160),
                SizeColumns.Text<ReferenceSite>("Value", site => site.TargetName, 110),
                SizeColumns.Text<ReferenceSite>("Note", site => site.Note ?? string.Empty, 140)
            }, 0, SortDirection.Ascending);
            _sites.Chosen += ReferenceSiteFinder.Reveal;
            _sites.ContextMenu = (site, menu) => menu.AppendAction("Open And Select", _ => ReferenceSiteFinder.Reveal(site));

            var details = Ui.Container("ba-pane");
            details.Add(_pathsTitle);
            details.Add(_paths);
            details.Add(_sitesTitle);
            details.Add(_sitesHost);

            var treePane = Ui.Container("ba-pane");
            treePane.Add(Ui.SectionTitle("Used by"));
            treePane.Add(_tree);

            var split = new TwoPaneSplitView(0, 640, TwoPaneSplitViewOrientation.Horizontal);
            split.AddToClassList("ba-split");
            split.Add(treePane);
            split.Add(details);

            _root.Add(filters);
            _root.Add(_evidence);
            _root.Add(split);
        }

        public string Title => "Usages";
        public VisualElement Root => _root;
        public bool CanExport => _assetPath != null;
        public CsvTable ExportCsv() => _paths.ToCsv();

        public void Refresh()
        {
            if (_assetPath != null)
                Show(_assetPath);
            else
                UpdateStatus();
        }

        public void Show(string assetPath)
        {
            _assetPath = string.IsNullOrEmpty(assetPath) || AssetDatabase.IsValidFolder(assetPath) ? null : assetPath;
            var asset = _assetPath != null ? AssetDatabase.LoadMainAssetAtPath(_assetPath) : null;
            _target.SetValueWithoutNotify(asset);
            _selected = null;
            _sitesHost.Clear();
            _sitesHost.Add(Ui.Label("Select a row in Used by to find the GameObject, component and field that hold the reference.", "ba-note"));
            _sitesTitle.text = "References inside";

            if (_assetPath == null)
            {
                _tree.SetRoots(Array.Empty<TreeNode<UsageNode>>(), false);
                _paths.SetItems(Array.Empty<UsagePath>());
                UpdateStatus();
                return;
            }

            var graph = DependencyGraph.Current ?? RebuildGraph();
            if (graph == null)
                return;

            var report = UsageResolver.Resolve(_assetPath, graph, _context.Player, _context.Layout);
            _evidence.text = DescribeEvidence(report);
            _paths.SetItems(report.Paths);
            _pathsTitle.text = report.PathsTruncated
                ? $"Paths from build roots (first {UsageResolver.MaxPaths})"
                : $"Paths from build roots ({report.Paths.Count})";
            _tree.SetRoots(BuildTree(graph, _assetPath), false);
            UpdateStatus();
        }

        private void UseSelection()
        {
            var path = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => !AssetDatabase.IsValidFolder(p));
            if (path != null)
                Show(path);
        }

        private void UpdateStatus()
        {
            var graph = DependencyGraph.Current;
            _status.text = graph == null
                ? "Dependency graph not built yet"
                : $"Graph: {graph.NodeCount} assets from {graph.RootCount} roots · built {graph.BuiltAt:HH:mm:ss}{(DependencyGraph.IsStale ? " · assets changed since, press Rebuild Graph" : string.Empty)}";
            _status.EnableInClassList("ba-text--warning", graph != null && DependencyGraph.IsStale);
        }

        private static DependencyGraph RebuildGraph()
        {
            try
            {
                return DependencyGraph.Rebuild((done, pending) => EditorUtility.DisplayCancelableProgressBar(
                    ProgressTitle, $"Scanning dependencies... {done} assets, {pending} queued", done / (float)(done + pending + 1)));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private IReadOnlyList<TreeNode<UsageNode>> BuildTree(DependencyGraph graph, string assetPath)
        {
            var budget = new TreeBudget();
            var ancestors = new HashSet<string> { assetPath };
            return graph.ReferencersOf(assetPath)
                .Select(referencer => BuildNode(graph, referencer, assetPath, ancestors, 1, budget))
                .ToList();
        }

        private TreeNode<UsageNode> BuildNode(DependencyGraph graph, string path, string referencedPath, HashSet<string> ancestors, int depth, TreeBudget budget)
        {
            budget.Remaining--;
            var children = new List<TreeNode<UsageNode>>();
            if (depth < MaxTreeDepth && budget.Remaining > 0)
            {
                ancestors.Add(path);
                foreach (var referencer in graph.ReferencersOf(path))
                {
                    if (budget.Remaining <= 0)
                        break;
                    if (!ancestors.Contains(referencer))
                        children.Add(BuildNode(graph, referencer, path, ancestors, depth + 1, budget));
                }

                ancestors.Remove(path);
            }

            var roots = string.Join(", ", graph.RootsAt(path).Select(root => root.Label).Distinct());
            var size = _context.BuildSizes.TryGetValue(path, out var entry) ? entry.Size : -1;
            var kind = AssetDatabase.GetMainAssetTypeAtPath(path)?.Name ?? "-";
            return new TreeNode<UsageNode>(new UsageNode(path, referencedPath, kind, roots, size), children);
        }

        private void ShowSites(UsageNode node)
        {
            _selected = node;
            AssetActions.Ping(node.Path);
            _sitesHost.Clear();
            _sitesTitle.text = $"References to {AssetPaths.DisplayName(node.ReferencedPath)} inside {node.Name}";

            if (ReferenceSiteFinder.IsScene(node.Path) && !ReferenceSiteFinder.IsSceneLoaded(node.Path))
            {
                var prompt = Ui.Container("ba-empty-state");
                prompt.Add(Ui.Empty("The scene is not open. Scanning opens it additively, then closes it again."));
                prompt.Add(Ui.Button("Scan Scene", () => ScanSites(node)));
                _sitesHost.Add(prompt);
                return;
            }

            ScanSites(node);
        }

        private void ScanSites(UsageNode node)
        {
            if (_selected != node)
                return;

            _sitesHost.Clear();
            IReadOnlyList<ReferenceSite> sites;
            try
            {
                EditorUtility.DisplayProgressBar(ProgressTitle, $"Scanning {node.Name}...", 0.5f);
                sites = ReferenceSiteFinder.Find(node.Path, node.ReferencedPath);
            }
            catch (Exception exception)
            {
                _sitesHost.Add(Ui.Empty($"Could not scan {node.Name}: {exception.Message}", HelpBoxMessageType.Error));
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (sites.Count == 0)
            {
                _sitesHost.Add(Ui.Empty("No serialized field points to it directly. The reference may live in data Unity does not expose to SerializedObject (e.g. scene lighting settings, sprite atlas packables, shader includes)."));
                return;
            }

            _sites.SetItems(sites);
            _sitesHost.Add(_sites);
        }

        private static string DescribeEvidence(UsageReport report)
        {
            var text = new StringBuilder(report.AssetPath);
            if (report.OwnRoots.Count > 0)
                text.Append($"\nIs a build root: {string.Join(", ", report.OwnRoots.Select(root => root.Label).Distinct())}");
            else if (report.IsReachable)
                text.Append($"\nReached from {report.Paths.Select(path => path.Root.AssetPath).Distinct().Count()} build root(s){(report.PathsTruncated ? "+" : string.Empty)}");
            else
                text.Append("\nNot referenced from any build root (scenes in build, Addressables, Resources, preloaded assets)");

            if (report.PlayerEntry != null)
            {
                text.Append($"\nPlayer build: {SizeFormat.Bytes(report.PlayerEntry.Size)} in {report.PlayerEntry.Container}");
                if (report.PlayerScenes.Count > 0)
                    text.Append($" · used by scenes {string.Join(", ", report.PlayerScenes.Select(AssetPaths.DisplayName))}");
            }

            foreach (var bundle in report.Bundles)
            {
                text.Append($"\nAddressables: {bundle.Group} / {bundle.Bundle}");
                if (bundle.IsImplicit)
                    text.Append(bundle.ReferencedBy.Count > 0
                        ? $" · implicit, pulled in by {string.Join(", ", bundle.ReferencedBy.Take(5).Select(AssetPaths.DisplayName))}{(bundle.ReferencedBy.Count > 5 ? $" +{bundle.ReferencedBy.Count - 5}" : string.Empty)}"
                        : " · implicit");
            }

            if (report.IsReachable && !report.IsInBuildData)
                text.Append("\nNot found in the loaded build data: the report may predate this reference, or the build stripped it.");
            else if (!report.IsReachable && report.OwnRoots.Count == 0 && report.IsInBuildData)
                text.Append("\nShipped, but no root found: it may come from Always Included Shaders, a build script, or the project changed since that build.");

            return text.ToString();
        }
    }
}
