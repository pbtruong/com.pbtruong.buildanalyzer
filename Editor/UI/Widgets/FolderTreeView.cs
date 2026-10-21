using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class FolderTreeView : VisualElement
    {
        private const string RootFolder = "Assets";
        private static readonly IComparer<string> FolderOrder = Comparer<string>.Create(EditorUtility.NaturalCompare);

        private sealed class FolderNode
        {
            public FolderNode(string path, int textureCount)
            {
                Path = path;
                TextureCount = textureCount;
            }

            public string Path { get; }
            public int TextureCount { get; }
            public string Name => AssetPaths.DisplayName(Path);
        }

        private readonly TreeView _tree;
        private readonly Toggle _showEmpty;
        private readonly Dictionary<string, int> _ids = new();
        private readonly Texture _folderIcon = AssetIcons.Builtin("Folder Icon");
        private string _selectedPath;
        private bool _rebuilding;

        public FolderTreeView()
        {
            AddToClassList("ba-folder-tree");

            _showEmpty = new Toggle("Show folders without textures");
            _showEmpty.AddToClassList("ba-inline-toggle");
            _showEmpty.RegisterValueChangedCallback(_ => Rebuild());
            Add(_showEmpty);

            _tree = new TreeView
            {
                fixedItemHeight = 18,
                selectionType = SelectionType.Single,
                makeItem = MakeRow,
                bindItem = BindRow
            };
            _tree.AddToClassList("ba-folder-tree__view");
            _tree.selectionChanged += items =>
            {
                if (_rebuilding || items.FirstOrDefault() is not FolderNode node || node.Path == _selectedPath)
                    return;
                _selectedPath = node.Path;
                FolderSelected?.Invoke(node.Path);
            };
            Add(_tree);
        }

        public event Action<string> FolderSelected;
        public string SelectedPath => _selectedPath;

        public void Rebuild()
        {
            var expanded = _ids.Where(pair => _tree.IsExpanded(pair.Value)).Select(pair => pair.Key).ToList();
            var counts = CountTextures();
            _ids.Clear();
            var nextId = 0;
            var roots = new List<TreeViewItemData<FolderNode>>();
            if (TryBuildNode(RootFolder, counts, ref nextId, out var root))
                roots.Add(root);

            _rebuilding = true;
            try
            {
                _tree.SetSelectionWithoutNotify(Array.Empty<int>());
                _tree.SetRootItems(roots);
                _tree.Rebuild();
                if (_ids.TryGetValue(RootFolder, out var rootId))
                    _tree.ExpandItem(rootId);
                foreach (var path in expanded.Where(_ids.ContainsKey))
                    _tree.ExpandItem(_ids[path]);

                if (_selectedPath != null && _ids.TryGetValue(_selectedPath, out var selectedId))
                    _tree.SetSelectionByIdWithoutNotify(new[] { selectedId });
            }
            finally
            {
                _rebuilding = false;
            }
        }

        public void Select(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                return;
            if (_ids.Count == 0)
                Rebuild();
            if (!_ids.ContainsKey(folderPath) && !_showEmpty.value)
            {
                _showEmpty.SetValueWithoutNotify(true);
                Rebuild();
            }
            if (!_ids.TryGetValue(folderPath, out var id))
                return;

            for (var parent = ParentOf(folderPath); parent != null; parent = ParentOf(parent))
            {
                if (_ids.TryGetValue(parent, out var parentId))
                    _tree.ExpandItem(parentId);
            }

            _tree.SetSelectionById(id);
            _tree.ScrollToItemById(id);
        }

        private bool TryBuildNode(string path, IReadOnlyDictionary<string, int> counts, ref int nextId, out TreeViewItemData<FolderNode> item)
        {
            var count = counts.TryGetValue(path, out var value) ? value : 0;
            if (count == 0 && !_showEmpty.value && path != RootFolder)
            {
                item = default;
                return false;
            }

            var id = nextId++;
            _ids[path] = id;
            var children = new List<TreeViewItemData<FolderNode>>();
            foreach (var subFolder in AssetDatabase.GetSubFolders(path).OrderBy(folder => folder, FolderOrder))
            {
                if (TryBuildNode(subFolder, counts, ref nextId, out var child))
                    children.Add(child);
            }

            item = new TreeViewItemData<FolderNode>(id, new FolderNode(path, count), children);
            return true;
        }

        private static Dictionary<string, int> CountTextures()
        {
            var counts = new Dictionary<string, int>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { RootFolder }))
            {
                for (var folder = ParentOf(AssetDatabase.GUIDToAssetPath(guid)); folder != null; folder = ParentOf(folder))
                    counts[folder] = counts.TryGetValue(folder, out var count) ? count + 1 : 1;
            }

            return counts;
        }

        private static string ParentOf(string path)
        {
            var slash = path.LastIndexOf('/');
            return slash > 0 ? path.Substring(0, slash) : null;
        }

        private VisualElement MakeRow()
        {
            var row = Ui.Container("ba-folder-row");
            var icon = new Image { image = _folderIcon, scaleMode = ScaleMode.ScaleToFit };
            icon.AddToClassList("ba-cell__icon");
            row.Add(icon);
            row.Add(Ui.Label(string.Empty, "ba-folder-row__name"));
            row.Add(Ui.Label(string.Empty, "ba-folder-row__count"));
            return row;
        }

        private void BindRow(VisualElement row, int index)
        {
            var node = _tree.GetItemDataForIndex<FolderNode>(index);
            row.Q<Label>(className: "ba-folder-row__name").text = node.Name;
            row.Q<Label>(className: "ba-folder-row__count").text = node.TextureCount > 0 ? node.TextureCount.ToString() : string.Empty;
            row.tooltip = node.Path;
        }
    }
}
