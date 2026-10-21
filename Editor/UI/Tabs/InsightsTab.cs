using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class InsightsTab : IAnalyzerTab
    {
        private static readonly InsightSeverity[] SeverityOrder = { InsightSeverity.Critical, InsightSeverity.Warning, InsightSeverity.Info };

        private readonly AnalyzerContext _context;
        private readonly VisualElement _root = Ui.Container("ba-page");
        private readonly VisualElement _body = Ui.Container("ba-body");
        private readonly Label _summary = Ui.Label(string.Empty, "ba-summary");
        private readonly Label _details = Ui.Label("Select an insight to see the full problem and suggestion.", "ba-details");
        private readonly DropdownField _rule;
        private readonly DropdownField _severity;
        private readonly SizeListView<SizeInsight> _table;
        private readonly List<string> _ruleNames;
        private string _search = string.Empty;

        public InsightsTab(AnalyzerContext context)
        {
            _context = context;
            _ruleNames = InsightAnalyzer.CreateRules().Select(rule => rule.Name).ToList();

            var filters = Ui.Container("ba-filterbar");
            filters.Add(Ui.Search(value =>
            {
                _search = value;
                ApplyFilter();
            }));
            _rule = Ui.Dropdown(null, Ui.WithAll("All rules", _ruleNames), _ => ApplyFilter());
            _severity = Ui.Dropdown(null, Ui.WithAll("All severities", SeverityOrder.Select(severity => severity.ToString())), _ => ApplyFilter());
            filters.Add(_rule);
            filters.Add(_severity);
            filters.Add(Ui.Button("Re-analyze", () =>
            {
                _context.InvalidateInsights();
                ApplyFilter();
            }, "Re-read import settings after changing textures/audio"));
            filters.Add(_summary);

            _table = new SizeListView<SizeInsight>(new[]
            {
                new SizeColumn<SizeInsight>("Level", 52, _ => string.Empty, CompareSeverityThenImpact)
                {
                    Icon = insight => SeverityIcon(insight.Severity),
                    CsvValue = insight => insight.Severity.ToString()
                },
                SizeColumns.Text<SizeInsight>("Rule", insight => insight.Rule, 140),
                SizeColumns.AssetName<SizeInsight>(insight => insight.Name, insight => insight.AssetPath, 200),
                SizeColumns.Bytes<SizeInsight>("Impact", insight => insight.Impact),
                SizeColumns.Text<SizeInsight>("Problem", insight => insight.Problem, 280, true),
                SizeColumns.Text<SizeInsight>("Suggestion", insight => insight.Suggestion, 280, true)
            }, 0, SortDirection.Descending);
            _table.Selected += insight =>
            {
                AssetActions.Ping(insight.AssetPath);
                _details.text = $"{insight.AssetPath}\nProblem: {insight.Problem}\nSuggestion: {insight.Suggestion}";
            };
            _table.Chosen += insight => AssetActions.Select(insight.AssetPath);
            _table.ContextMenu = (insight, menu) => AssetActions.AppendMenu(menu, insight.AssetPath);

            _body.Add(filters);
            _body.Add(_table);
            _body.Add(_details);
        }

        public string Title => "Insights";
        public VisualElement Root => _root;
        public bool CanExport => _context.HasData;
        public CsvTable ExportCsv() => _table.ToCsv();

        public void Refresh()
        {
            _root.Clear();
            if (!_context.HasData)
            {
                _root.Add(Ui.Empty("No build data to analyze yet."));
                return;
            }

            _root.Add(_body);
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (!_context.HasData)
                return;

            var insights = _context.Insights;
            var filtered = insights.Where(insight =>
                    (_rule.index <= 0 || insight.Rule == _ruleNames[_rule.index - 1])
                    && (_severity.index <= 0 || insight.Severity == SeverityOrder[_severity.index - 1])
                    && Ui.Matches(_search, insight.AssetPath, insight.Problem, insight.Suggestion))
                .ToList();
            _table.SetItems(filtered);

            var counts = SeverityOrder.Select(severity => $"{insights.Count(insight => insight.Severity == severity)} {severity.ToString().ToLowerInvariant()}");
            _summary.text = $"{string.Join(" · ", counts)} · showing {filtered.Count}";
        }

        private static int CompareSeverityThenImpact(SizeInsight a, SizeInsight b) =>
            a.Severity != b.Severity ? a.Severity.CompareTo(b.Severity) : a.Impact.CompareTo(b.Impact);

        private static Texture SeverityIcon(InsightSeverity severity) => severity switch
        {
            InsightSeverity.Critical => AssetIcons.Builtin("console.erroricon.sml"),
            InsightSeverity.Warning => AssetIcons.Builtin("console.warnicon.sml"),
            _ => AssetIcons.Builtin("console.infoicon.sml")
        };
    }
}
