using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace PBTruong.BuildAnalyzer.Editor
{
    public sealed class SizeListView<T> : VisualElement
    {
        private readonly MultiColumnListView _list;
        private readonly IReadOnlyList<SizeColumn<T>> _columns;
        private readonly List<T> _items = new();
        private IReadOnlyList<T> _source = Array.Empty<T>();

        public SizeListView(IReadOnlyList<SizeColumn<T>> columns, int defaultSortColumn, SortDirection defaultDirection)
        {
            _columns = columns;
            AddToClassList("ba-table");

            _list = new MultiColumnListView
            {
                itemsSource = _items,
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
            _list.AddToClassList("ba-table__view");

            for (var i = 0; i < columns.Count; i++)
                _list.columns.Add(SizeCells.CreateColumn(columns[i], i, row => _items[row]));

            _list.sortColumnDescriptions.Add(new SortColumnDescription(defaultSortColumn, defaultDirection));
            _list.columnSortingChanged += ApplySort;
            _list.selectionChanged += _ => { if (_list.selectedItem is T item) Selected?.Invoke(item); };
            _list.itemsChosen += chosen => { if (chosen.FirstOrDefault() is T item) Chosen?.Invoke(item); };
            _list.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                if (_list.selectedItem is T item)
                    ContextMenu?.Invoke(item, evt.menu);
            }));

            Add(_list);
        }

        public event Action<T> Selected;
        public event Action<T> Chosen;
        public Action<T, DropdownMenu> ContextMenu { get; set; }
        public IReadOnlyList<T> Items => _items;

        public void SetItems(IReadOnlyList<T> items)
        {
            _source = items;
            ApplySort();
        }

        public void RefreshItems() => _list.RefreshItems();

        public CsvTable ToCsv() => new(
            _columns.Where(column => !column.IsToggle).Select(column => column.Title).ToArray(),
            _items.Select(item => _columns.Where(column => !column.IsToggle).Select(column => column.CsvText(item)).ToArray()).ToList());

        private void ApplySort()
        {
            _items.Clear();
            _items.AddRange(_source);
            if (SizeCells.TryGetSort(_list.sortedColumns, _list.sortColumnDescriptions, out var index, out var direction)
                && index < _columns.Count && _columns[index].Compare != null)
            {
                _items.Sort(SizeCells.Directed(_columns[index].Compare, direction));
            }

            _list.ClearSelection();
            _list.Rebuild();
        }
    }
}
