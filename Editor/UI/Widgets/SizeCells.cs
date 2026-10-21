using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal static class SizeCells
    {
        private const string ColumnNamePrefix = "c";

        public static Column CreateColumn<T>(SizeColumn<T> column, int index, Func<int, T> itemAt) => new()
        {
            name = ColumnNamePrefix + index,
            title = column.Title,
            width = column.Width,
            minWidth = 40,
            stretchable = column.Stretch,
            sortable = column.Compare != null,
            makeCell = () => Make(column),
            bindCell = (element, row) => Bind(element, column, itemAt(row))
        };

        public static bool TryGetSort(IEnumerable<SortColumnDescription> sorted, IEnumerable<SortColumnDescription> declared, out int index, out SortDirection direction)
        {
            foreach (var description in sorted.Concat(declared))
            {
                index = ResolveIndex(description);
                if (index < 0)
                    continue;
                direction = description.direction;
                return true;
            }

            index = -1;
            direction = SortDirection.Ascending;
            return false;
        }

        public static Comparison<T> Directed<T>(Comparison<T> compare, SortDirection direction) =>
            direction == SortDirection.Ascending ? compare : (a, b) => compare(b, a);

        private static int ResolveIndex(SortColumnDescription description)
        {
            var name = description.column != null ? description.column.name : description.columnName;
            if (!string.IsNullOrEmpty(name) && name.StartsWith(ColumnNamePrefix, StringComparison.Ordinal) && int.TryParse(name.Substring(1), out var parsed))
                return parsed;
            return description.columnIndex;
        }

        private static VisualElement Make<T>(SizeColumn<T> column)
        {
            var cell = new VisualElement();
            cell.AddToClassList("ba-cell");
            if (column.IsToggle)
            {
                var toggle = new Toggle();
                toggle.AddToClassList("ba-cell__toggle");
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (toggle.userData is T item)
                        column.SetChecked(item, evt.newValue);
                });
                cell.Add(toggle);
                return cell;
            }

            if (column.Icon != null)
            {
                var icon = new Image { scaleMode = ScaleMode.ScaleToFit };
                icon.AddToClassList("ba-cell__icon");
                cell.Add(icon);
            }

            var label = new Label();
            label.AddToClassList("ba-cell__label");
            if (column.Numeric)
                label.AddToClassList("ba-cell__label--numeric");
            cell.Add(label);
            return cell;
        }

        private static void Bind<T>(VisualElement cell, SizeColumn<T> column, T item)
        {
            if (column.IsToggle)
            {
                var toggle = cell.Q<Toggle>();
                toggle.userData = item;
                toggle.SetValueWithoutNotify(column.IsChecked(item));
                return;
            }

            var label = cell.Q<Label>();
            var text = column.Text(item);
            label.text = text;
            label.tooltip = text;

            if (column.Icon != null)
                cell.Q<Image>().image = column.Icon(item);

            if (cell.userData is string previousClass)
                label.RemoveFromClassList(previousClass);
            var extraClass = column.LabelClass?.Invoke(item);
            if (!string.IsNullOrEmpty(extraClass))
                label.AddToClassList(extraClass);
            cell.userData = extraClass;
        }
    }
}
