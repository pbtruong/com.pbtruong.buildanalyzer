using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace PBTruong.BuildAnalyzer.Editor
{
    public sealed class TreeNode<T>
    {
        public TreeNode(T value, IReadOnlyList<TreeNode<T>> children = null)
        {
            Value = value;
            Children = children ?? Array.Empty<TreeNode<T>>();
        }

        public T Value { get; }
        public IReadOnlyList<TreeNode<T>> Children { get; }
    }

    public sealed class SizeTreeView<T> : VisualElement
    {
        private readonly MultiColumnTreeView _tree;
        private readonly IReadOnlyList<SizeColumn<T>> _columns;
        private IReadOnlyList<TreeNode<T>> _roots = Array.Empty<TreeNode<T>>();
        private bool _expandAll;

        public SizeTreeView(IReadOnlyList<SizeColumn<T>> columns, int defaultSortColumn, SortDirection defaultDirection)
        {
            _columns = columns;
            AddToClassList("ba-table");

            _tree = new MultiColumnTreeView
            {
#if UNITY_2023_2_OR_NEWER
                sortingMode = ColumnSortingMode.Custom,
#else
                sortingEnabled = true,
#endif
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                fixedItemHeight = 20,
                showBorder = true
            };
            _tree.AddToClassList("ba-table__view");

            for (var i = 0; i < columns.Count; i++)
                _tree.columns.Add(SizeCells.CreateColumn(columns[i], i, row => _tree.GetItemDataForIndex<T>(row)));

            _tree.sortColumnDescriptions.Add(new SortColumnDescription(defaultSortColumn, defaultDirection));
            _tree.columnSortingChanged += RebuildTree;
            _tree.selectionChanged += _ => { if (_tree.selectedItem is T item) Selected?.Invoke(item); };
            _tree.itemsChosen += chosen => { if (chosen.FirstOrDefault() is T item) Chosen?.Invoke(item); };
            _tree.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                if (_tree.selectedItem is T item)
                    ContextMenu?.Invoke(item, evt.menu);
            }));

            Add(_tree);
        }

        public event Action<T> Selected;
        public event Action<T> Chosen;
        public Action<T, DropdownMenu> ContextMenu { get; set; }

        public void SetRoots(IReadOnlyList<TreeNode<T>> roots, bool expandAll)
        {
            _roots = roots;
            _expandAll = expandAll;
            RebuildTree();
        }

        private void RebuildTree()
        {
            Comparison<T> compare = null;
            if (SizeCells.TryGetSort(_tree.sortedColumns, _tree.sortColumnDescriptions, out var index, out var direction)
                && index < _columns.Count && _columns[index].Compare != null)
            {
                compare = SizeCells.Directed(_columns[index].Compare, direction);
            }

            var nextId = 0;
            _tree.ClearSelection();
            _tree.SetRootItems(Convert(_roots, compare, ref nextId));
            _tree.Rebuild();
            if (_expandAll)
                _tree.ExpandAll();
            else
                _tree.ExpandRootItems();
        }

        private static List<TreeViewItemData<T>> Convert(IEnumerable<TreeNode<T>> nodes, Comparison<T> compare, ref int nextId)
        {
            var ordered = nodes.ToList();
            if (compare != null)
                ordered.Sort((a, b) => compare(a.Value, b.Value));

            var result = new List<TreeViewItemData<T>>(ordered.Count);
            foreach (var node in ordered)
            {
                var id = nextId++;
                var children = node.Children.Count > 0 ? Convert(node.Children, compare, ref nextId) : null;
                result.Add(new TreeViewItemData<T>(id, node.Value, children));
            }

            return result;
        }
    }
}
