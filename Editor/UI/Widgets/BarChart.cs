using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace PBTruong.BuildAnalyzer.Editor
{
    public readonly struct BarSegment
    {
        public BarSegment(long value, string cssClass)
        {
            Value = value;
            CssClass = cssClass;
        }

        public long Value { get; }
        public string CssClass { get; }
    }

    public sealed class BarRow
    {
        public BarRow(string label, IReadOnlyList<BarSegment> segments, string valueText, string tooltip = null)
        {
            Label = label;
            Segments = segments;
            ValueText = valueText;
            Tooltip = tooltip;
        }

        public string Label { get; }
        public IReadOnlyList<BarSegment> Segments { get; }
        public string ValueText { get; }
        public string Tooltip { get; }
        public long Total => Segments.Sum(segment => segment.Value);
    }

    public sealed class BarChart : VisualElement
    {
        public BarChart(IReadOnlyList<BarRow> rows)
        {
            AddToClassList("ba-bars");
            var max = rows.Count == 0 ? 0 : rows.Max(row => row.Total);

            foreach (var row in rows)
            {
                var line = new VisualElement { tooltip = row.Tooltip };
                line.AddToClassList("ba-bars__row");
                line.Add(Ui.Label(row.Label, "ba-bars__label"));

                var track = new VisualElement();
                track.AddToClassList("ba-bars__track");
                foreach (var segment in row.Segments)
                {
                    if (segment.Value <= 0 || max <= 0)
                        continue;
                    var fill = new VisualElement();
                    fill.AddToClassList("ba-bars__fill");
                    fill.AddToClassList(segment.CssClass);
                    fill.style.width = Length.Percent(100f * segment.Value / max);
                    track.Add(fill);
                }

                line.Add(track);
                line.Add(Ui.Label(row.ValueText, "ba-bars__value"));
                Add(line);
            }
        }
    }
}
