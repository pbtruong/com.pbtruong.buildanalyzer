using System;
using UnityEngine;

namespace PBTruong.BuildAnalyzer.Editor
{
    public sealed class SizeColumn<T>
    {
        public SizeColumn(string title, float width, Func<T, string> text, Comparison<T> compare = null)
        {
            Title = title;
            Width = width;
            Text = text;
            Compare = compare;
        }

        public string Title { get; }
        public float Width { get; }
        public Func<T, string> Text { get; }
        public Comparison<T> Compare { get; }
        public bool Stretch { get; set; }
        public bool Numeric { get; set; }
        public Func<T, Texture> Icon { get; set; }
        public Func<T, string> LabelClass { get; set; }
        public Func<T, string> CsvValue { get; set; }
        public Func<T, bool> IsChecked { get; set; }
        public Action<T, bool> SetChecked { get; set; }
        public bool IsToggle => IsChecked != null;

        public string CsvText(T item) => (CsvValue ?? Text)(item);
    }

    public static class SizeColumns
    {
        public static SizeColumn<T> AssetName<T>(Func<T, string> name, Func<T, string> assetPath, float width = 220) =>
            new("Name", width, name, (a, b) => string.Compare(name(a), name(b), StringComparison.OrdinalIgnoreCase))
            {
                Stretch = true,
                Icon = item => AssetIcons.For(assetPath(item)),
                CsvValue = assetPath
            };

        public static SizeColumn<T> Bytes<T>(string title, Func<T, long> size, float width = 85) =>
            new(title, width, item => SizeFormat.Bytes(size(item)), (a, b) => size(a).CompareTo(size(b)))
            {
                Numeric = true,
                CsvValue = item => SizeFormat.Raw(size(item))
            };

        public static SizeColumn<T> Text<T>(string title, Func<T, string> text, float width = 110, bool stretch = false) =>
            new(title, width, text, (a, b) => string.Compare(text(a), text(b), StringComparison.OrdinalIgnoreCase))
            {
                Stretch = stretch
            };

        public static SizeColumn<T> Toggle<T>(Func<T, bool> isChecked, Action<T, bool> setChecked, float width = 26) =>
            new(string.Empty, width, item => isChecked(item) ? "1" : "0", (a, b) => isChecked(a).CompareTo(isChecked(b)))
            {
                IsChecked = isChecked,
                SetChecked = setChecked
            };

        public static SizeColumn<T> Number<T>(string title, Func<T, int> value, float width = 60) =>
            new(title, width, item => value(item).ToString(), (a, b) => value(a).CompareTo(value(b)))
            {
                Numeric = true
            };
    }
}
