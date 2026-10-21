using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal static class Ui
    {
        public static Label Label(string text, string cssClass = null)
        {
            var label = new Label(text);
            if (cssClass != null)
                label.AddToClassList(cssClass);
            return label;
        }

        public static VisualElement Container(string cssClass)
        {
            var element = new VisualElement();
            element.AddToClassList(cssClass);
            return element;
        }

        public static VisualElement Card(string title, string value, string hint = null, string valueClass = null)
        {
            var card = Container("ba-card");
            card.Add(Label(title.ToUpperInvariant(), "ba-card__title"));
            var valueLabel = Label(value, "ba-card__value");
            if (valueClass != null)
                valueLabel.AddToClassList(valueClass);
            card.Add(valueLabel);
            if (!string.IsNullOrEmpty(hint))
                card.Add(Label(hint, "ba-card__hint"));
            return card;
        }

        public static Label SectionTitle(string text) => Label(text, "ba-section-title");

        public static VisualElement Empty(string message, HelpBoxMessageType type = HelpBoxMessageType.Info)
        {
            var box = new HelpBox(message, type);
            box.AddToClassList("ba-empty");
            return box;
        }

        public static Button Button(string text, Action onClick, string tooltip = null)
        {
            var button = new Button(onClick) { text = text, tooltip = tooltip };
            button.AddToClassList("ba-button");
            return button;
        }

        public static VisualElement Legend(params (string text, string cssClass)[] items)
        {
            var legend = Container("ba-legend");
            foreach (var (text, cssClass) in items)
            {
                var swatch = Container("ba-legend__swatch");
                swatch.AddToClassList(cssClass);
                legend.Add(swatch);
                legend.Add(Label(text, "ba-legend__label"));
            }

            return legend;
        }

        public static DropdownField Dropdown(string label, List<string> choices, Action<int> onChanged)
        {
            var dropdown = new DropdownField(label, choices, 0);
            dropdown.AddToClassList("ba-dropdown");
            dropdown.RegisterValueChangedCallback(_ => onChanged(dropdown.index));
            return dropdown;
        }

        public static bool Matches(string search, params string[] values)
        {
            if (string.IsNullOrEmpty(search))
                return true;
            foreach (var value in values)
            {
                if (!string.IsNullOrEmpty(value) && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        public static List<string> WithAll(string allLabel, IEnumerable<string> values)
        {
            var choices = new List<string> { allLabel };
            choices.AddRange(values);
            return choices;
        }

        public static string DeltaClass(long delta) =>
            delta > 0 ? "ba-delta--up" : delta < 0 ? "ba-delta--down" : null;

        public static ToolbarSearchField Search(Action<string> onChanged)
        {
            var field = new ToolbarSearchField();
            field.AddToClassList("ba-search");
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue ?? string.Empty));
            return field;
        }
    }

    internal static class AssetIcons
    {
        public static Texture For(string assetPath) =>
            string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.GetCachedIcon(assetPath);

        public static Texture Builtin(string name) => EditorGUIUtility.IconContent(name).image;
    }

    internal static class AssetActions
    {
        public static void Ping(string assetPath)
        {
            var asset = Load(assetPath);
            if (asset != null)
                EditorGUIUtility.PingObject(asset);
        }

        public static void Select(string assetPath)
        {
            var asset = Load(assetPath);
            if (asset == null)
                return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        public static void AppendMenu(DropdownMenu menu, string assetPath)
        {
            var status = Load(assetPath) != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
            menu.AppendAction("Select In Project", _ => Select(assetPath), status);
            menu.AppendAction("Show In Explorer", _ => EditorUtility.RevealInFinder(assetPath), status);
            menu.AppendAction("Copy Path", _ => EditorGUIUtility.systemCopyBuffer = assetPath);
            menu.AppendSeparator();
            menu.AppendAction("Find Usages In Build", _ => BuildAnalyzerWindow.ShowUsages(assetPath), status);
        }

        private static UnityEngine.Object Load(string assetPath) =>
            string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadMainAssetAtPath(assetPath);
    }
}
