using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class TexturesTab : IAnalyzerTab
    {
        private const string FolderPrefKey = "PBTruong.BuildAnalyzer.TextureFolder";
        private const string SubfoldersPrefKey = "PBTruong.BuildAnalyzer.TextureSubfolders";
        private const string Keep = "(keep)";

        private static readonly int[] MaxSizes = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384 };
        private static readonly (string label, int value)[] Qualities = { ("Fast", 0), ("Normal", 50), ("Best", 100) };
        private static readonly (string label, TextureImporterCompression value)[] Compressions =
        {
            ("None", TextureImporterCompression.Uncompressed),
            ("Low Quality", TextureImporterCompression.CompressedLQ),
            ("Normal Quality", TextureImporterCompression.Compressed),
            ("High Quality", TextureImporterCompression.CompressedHQ)
        };
        private static readonly TextureResizeAlgorithm[] ResizeAlgorithms = { TextureResizeAlgorithm.Mitchell, TextureResizeAlgorithm.Bilinear };

        private readonly AnalyzerContext _context;
        private readonly VisualElement _root = Ui.Container("ba-page");
        private readonly FolderTreeView _folderTree = new();
        private readonly Label _folderLabel = Ui.Label("Pick a folder in the tree", "ba-field-label");
        private readonly Toggle _subfolders;
        private readonly DropdownField _type;
        private readonly ToolbarToggle _inBuildOnly;
        private readonly ToolbarToggle _hidePacked;
        private readonly Label _summary = Ui.Label(string.Empty, "ba-summary");
        private readonly Label _selection = Ui.Label(string.Empty, "ba-summary");
        private readonly SizeListView<TextureRow> _table;
        private readonly VisualElement _platformButtons = Ui.Container("ba-modes");
        private readonly VisualElement _fields = Ui.Container("ba-filterbar");
        private readonly Button _apply;
        private readonly IReadOnlyList<BuildPlatform> _platforms = BuildPlatforms.Installed();
        private readonly Dictionary<string, TextureCompressionPlan> _plans = new();
        private readonly List<TextureRow> _rows = new();
        private List<TextureRow> _visible = new();
        private BuildPlatform _platform;
        private string _search = string.Empty;
        private bool _folderRestored;
        private string _folderPath;

        public TexturesTab(AnalyzerContext context)
        {
            _context = context;
            _platform = _platforms[0];
            ResetPlans();

            var source = Ui.Container("ba-filterbar");
            _folderTree.FolderSelected += folder =>
            {
                _folderPath = folder;
                LoadRows();
            };
            _subfolders = new Toggle("Subfolders") { value = EditorPrefs.GetBool(SubfoldersPrefKey, true) };
            _subfolders.AddToClassList("ba-inline-toggle");
            _subfolders.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetBool(SubfoldersPrefKey, evt.newValue);
                LoadRows();
            });
            source.Add(_folderLabel);
            source.Add(Ui.Button("Use Selection", UseSelectedFolder, "Jump to the folder selected in the Project window"));
            source.Add(_subfolders);
            source.Add(Ui.Button("Rescan", () =>
            {
                _folderTree.Rebuild();
                LoadRows();
            }, "Re-read folders, textures and their import settings"));
            source.Add(_summary);

            var filters = Ui.Container("ba-filterbar");
            filters.Add(Ui.Search(value =>
            {
                _search = value;
                ApplyFilter();
            }));
            _type = Ui.Dropdown(null, Ui.WithAll("All types", Enum.GetNames(typeof(TextureImporterType))), _ => ApplyFilter());
            filters.Add(_type);
            _inBuildOnly = new ToolbarToggle { text = "In build only", tooltip = "Only textures found in the loaded build data" };
            _inBuildOnly.RegisterValueChangedCallback(_ => ApplyFilter());
            filters.Add(_inBuildOnly);
            _hidePacked = new ToolbarToggle { text = "Hide atlas sprites", tooltip = "Hide textures packed into a Sprite Atlas (the atlas settings decide their compression)" };
            _hidePacked.RegisterValueChangedCallback(_ => ApplyFilter());
            filters.Add(_hidePacked);
            filters.Add(Ui.Button("All", () => SetChecked(row => !row.IsPacked), "Select every visible texture that is not packed in an atlas"));
            filters.Add(Ui.Button("None", () => SetChecked(_ => false)));
            filters.Add(Ui.Button("Invert", () => SetChecked(row => !row.IsChecked)));
            filters.Add(_selection);

            _table = new SizeListView<TextureRow>(new[]
            {
                SizeColumns.Toggle<TextureRow>(row => row.IsChecked, (row, value) =>
                {
                    row.IsChecked = value;
                    UpdateSelectionSummary();
                }),
                SizeColumns.AssetName<TextureRow>(row => row.Name, row => row.Path, 200),
                SizeColumns.Text<TextureRow>("Type", row => row.TypeName, 70),
                new SizeColumn<TextureRow>("Source", 80, row => row.SourceSize, (a, b) => ((long)a.Width * a.Height).CompareTo((long)b.Width * b.Height)) { Numeric = true },
                OptionalBytes("Build Size", row => row.BuildSize),
                SizeColumns.Text<TextureRow>("Current", row => row.CurrentText, 170),
                OptionalBytes("Est. Now", row => row.CurrentBytes),
                new SizeColumn<TextureRow>("New", 170, row => row.NewText, (a, b) => string.CompareOrdinal(a.NewText, b.NewText))
                {
                    LabelClass = row => row.WillChange ? "ba-text--changed" : null
                },
                OptionalBytes("Est. New", row => row.NewBytes),
                SizeColumns.Text<TextureRow>("Flags", row => row.Flags, 200, true)
            }, 4, SortDirection.Descending);
            _table.Selected += row => AssetActions.Ping(row.Path);
            _table.Chosen += row => AssetActions.Select(row.Path);
            _table.ContextMenu = (row, menu) => AssetActions.AppendMenu(menu, row.Path);

            var panel = Ui.Container("ba-panel");
            var header = Ui.Container("ba-filterbar");
            header.Add(Ui.Label("Compression", "ba-field-label"));
            foreach (var platform in _platforms)
            {
                var button = new Button(() => SelectPlatform(platform)) { text = platform.Label, userData = platform };
                button.AddToClassList("ba-mode");
                _platformButtons.Add(button);
            }

            header.Add(_platformButtons);
            header.Add(Ui.Button("Reset", () =>
            {
                ResetPlans();
                BuildFields();
                RecomputePreview();
            }, "Clear the pending changes of every platform"));
            _apply = Ui.Button("Apply", ApplyChanges);
            _apply.AddToClassList("ba-button--primary");
            header.Add(_apply);
            panel.Add(header);
            panel.Add(_fields);
            panel.Add(Ui.Label("Est. = in-memory size from source size, max size and format bits per pixel (crunched is approximate). Build Size is from the loaded build and only changes after the next build. Pending changes of every platform are applied together.", "ba-note"));

            var content = Ui.Container("ba-pane");
            content.Add(source);
            content.Add(filters);
            content.Add(_table);

            var split = new TwoPaneSplitView(0, 280, TwoPaneSplitViewOrientation.Horizontal);
            split.AddToClassList("ba-split");
            split.Add(_folderTree);
            split.Add(content);

            _root.Add(split);
            _root.Add(panel);

            SelectPlatform(_platform);
        }

        public string Title => "Textures";
        public VisualElement Root => _root;
        public bool CanExport => _rows.Count > 0;
        public CsvTable ExportCsv() => _table.ToCsv();

        public void Refresh()
        {
            if (!_folderRestored)
            {
                _folderRestored = true;
                RestoreFolder();
                return;
            }

            UpdateBuildSizes();
            RecomputePreview();
        }

        private static SizeColumn<TextureRow> OptionalBytes(string title, Func<TextureRow, long> size) =>
            new(title, 80, row => size(row) < 0 ? "-" : SizeFormat.Bytes(size(row)), (a, b) => size(a).CompareTo(size(b)))
            {
                Numeric = true,
                CsvValue = row => SizeFormat.Raw(size(row))
            };

        private void RestoreFolder()
        {
            _folderTree.Rebuild();
            var saved = EditorPrefs.GetString(FolderPrefKey, string.Empty);
            if (AssetDatabase.IsValidFolder(saved))
                _folderTree.Select(saved);
            else
                UpdateSummary();
        }

        private void UseSelectedFolder()
        {
            var folder = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(AssetDatabase.IsValidFolder);
            if (folder != null)
                _folderTree.Select(folder);
        }

        private void LoadRows()
        {
            _rows.Clear();
            var folder = _folderPath;
            _folderLabel.text = folder ?? "Pick a folder in the tree";
            if (folder == null || !AssetDatabase.IsValidFolder(folder))
            {
                ApplyFilter();
                return;
            }

            EditorPrefs.SetString(FolderPrefKey, folder);
            var atlases = SpriteAtlasMembership.Build();
            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .Where(path => _subfolders.value || Path.GetDirectoryName(path)?.Replace('\\', '/') == folder);

            foreach (var path in paths)
            {
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                    _rows.Add(new TextureRow(path, importer, atlases.TryGetValue(path, out var atlas) ? atlas : null));
            }

            UpdateBuildSizes();
            RecomputePreview();
        }

        private void UpdateBuildSizes()
        {
            var sizes = _context.BuildSizes;
            foreach (var row in _rows)
                row.BuildSize = sizes.TryGetValue(row.Path, out var entry) ? entry.Size : -1;
        }

        private void ApplyFilter()
        {
            var type = _type.index <= 0 ? null : _type.value;
            _visible = _rows.Where(row =>
                    (type == null || row.TypeName == type)
                    && (!_inBuildOnly.value || row.BuildSize >= 0)
                    && (!_hidePacked.value || !row.IsPacked)
                    && Ui.Matches(_search, row.Path, row.Atlas, row.Flags))
                .ToList();
            _table.SetItems(_visible);
            UpdateSummary();
            UpdateSelectionSummary();
        }

        private void UpdateSummary()
        {
            _summary.text = _folderPath == null
                ? "Pick a folder to list its textures"
                : $"{_visible.Count} / {_rows.Count} textures · build {SizeFormat.Bytes(_visible.Where(row => row.BuildSize > 0).Sum(row => row.BuildSize))}";
        }

        private void SetChecked(Func<TextureRow, bool> value)
        {
            foreach (var row in _visible)
                row.IsChecked = value(row);
            _table.RefreshItems();
            UpdateSelectionSummary();
        }

        private void UpdateSelectionSummary()
        {
            var selected = _rows.Where(row => row.IsChecked).ToList();
            var delta = selected.Where(row => row.WillChange && row.CurrentBytes >= 0 && row.NewBytes >= 0).Sum(row => row.NewBytes - row.CurrentBytes);
            _selection.text = selected.Count == 0
                ? "Nothing selected"
                : $"{selected.Count} selected · build {SizeFormat.Bytes(selected.Where(row => row.BuildSize > 0).Sum(row => row.BuildSize))} · est. {_platform.Label} {SizeFormat.Signed(delta)}";

            var pending = _plans.Values.Where(plan => plan.HasChanges).ToList();
            _apply.text = pending.Count == 0
                ? "Apply"
                : $"Apply {string.Join(", ", pending.Select(plan => PlatformLabel(plan.Platform)))} to {selected.Count}";
            _apply.SetEnabled(selected.Count > 0 && pending.Count > 0);
        }

        private void ResetPlans()
        {
            _plans.Clear();
            foreach (var platform in _platforms)
                _plans[platform.Name] = new TextureCompressionPlan(platform.Name);
        }

        private string PlatformLabel(string name) => _platforms.FirstOrDefault(platform => platform.Name == name)?.Label ?? name;

        private void SelectPlatform(BuildPlatform platform)
        {
            _platform = platform;
            foreach (var button in _platformButtons.Children().OfType<Button>())
            {
                var buttonPlatform = (BuildPlatform)button.userData;
                button.EnableInClassList("ba-mode--selected", buttonPlatform == platform);
                button.text = _plans[buttonPlatform.Name].HasChanges ? buttonPlatform.Label + " *" : buttonPlatform.Label;
            }

            BuildFields();
            RecomputePreview();
        }

        private void BuildFields()
        {
            _fields.Clear();
            var plan = _plans[_platform.Name];
            if (plan.IsDefault)
            {
                _fields.Add(Choice("Compression", Compressions.Select(option => option.label), IndexOf(Compressions.Select(option => option.value), plan.Compression),
                    index => plan.Compression = index < 0 ? null : Compressions[index].value));
                _fields.Add(Choice("Crunch", new[] { "On", "Off" }, plan.Crunched.HasValue ? (plan.Crunched.Value ? 0 : 1) : -1,
                    index => plan.Crunched = index < 0 ? null : index == 0));
            }
            else
            {
                var formats = TextureFormats.ForPlatform(plan.Platform);
                _fields.Add(Choice("Override", new[] { "Override", "Remove override" }, plan.Override == OverrideChange.Keep ? -1 : (int)plan.Override - 1,
                    index => plan.Override = index < 0 ? OverrideChange.Keep : (OverrideChange)(index + 1)));
                _fields.Add(Choice("Format", formats.Select(TextureFormats.Label), IndexOf(formats, plan.Format),
                    index => plan.Format = index < 0 ? null : formats[index]));
            }

            _fields.Add(Choice("Max Size", MaxSizes.Select(size => size.ToString()), IndexOf(MaxSizes, plan.MaxSize),
                index => plan.MaxSize = index < 0 ? null : MaxSizes[index]));
            _fields.Add(Choice("Quality", Qualities.Select(option => option.label), IndexOf(Qualities.Select(option => option.value), plan.Quality),
                index => plan.Quality = index < 0 ? null : Qualities[index].value));
            _fields.Add(Choice("Resize", ResizeAlgorithms.Select(algorithm => algorithm.ToString()), IndexOf(ResizeAlgorithms, plan.Resize),
                index => plan.Resize = index < 0 ? null : ResizeAlgorithms[index]));
        }

        private DropdownField Choice(string label, IEnumerable<string> options, int selected, Action<int> onChanged)
        {
            var choices = new List<string> { Keep };
            choices.AddRange(options);
            var dropdown = new DropdownField(label, choices, selected + 1);
            dropdown.AddToClassList("ba-dropdown");
            dropdown.AddToClassList("ba-choice");
            dropdown.RegisterValueChangedCallback(_ =>
            {
                onChanged(dropdown.index - 1);
                SelectPlatform(_platform);
            });
            return dropdown;
        }

        private static int IndexOf<T>(IEnumerable<T> values, T? value) where T : struct =>
            value.HasValue ? values.ToList().IndexOf(value.Value) : -1;

        private void RecomputePreview()
        {
            var plan = _plans[_platform.Name];
            var estimatePlatform = _platform.IsDefault ? BuildPlatforms.NameOf(_platform.Target) : _platform.Name;
            foreach (var row in _rows)
                UpdatePreview(row, plan, estimatePlatform);
            ApplyFilter();
        }

        private void UpdatePreview(TextureRow row, TextureCompressionPlan plan, string estimatePlatform)
        {
            var importer = row.Importer;
            var current = plan.Current(importer);
            var preview = plan.Preview(importer);
            row.WillChange = !TextureCompressionPlan.SameSettings(current, preview);
            row.CurrentText = Describe(current, plan.IsDefault);
            row.NewText = row.WillChange ? Describe(preview, plan.IsDefault) : string.Empty;

            var currentFormat = EffectiveFormat(row, current, current, plan.IsDefault, estimatePlatform);
            var newFormat = row.WillChange ? EffectiveFormat(row, current, preview, plan.IsDefault, estimatePlatform) : currentFormat;
            var currentMax = EffectiveMaxSize(row, current, plan.IsDefault, estimatePlatform);
            var newMax = EffectiveMaxSize(row, preview, plan.IsDefault, estimatePlatform);
            row.CurrentBytes = Estimate(row, currentMax, currentFormat);
            row.NewBytes = row.WillChange ? Estimate(row, newMax, newFormat) : TextureFormats.Unknown;

            var flags = new List<string>();
            if (row.IsPacked)
                flags.Add($"Atlas: {row.Atlas}");
            if (_context.HasData && row.BuildSize < 0)
                flags.Add("Not in build");
            var shown = (newFormat ?? currentFormat) ?? TextureImporterFormat.Automatic;
            if (TextureFormats.IsBlockCompressed(shown) && (row.Width % 4 != 0 || row.Height % 4 != 0))
                flags.Add("Size not multiple of 4");
            if (importer.mipmapEnabled && importer.textureType == TextureImporterType.Sprite)
                flags.Add("Mipmaps on sprite");
            row.Flags = string.Join(" · ", flags);
        }

        private static string Describe(TextureImporterPlatformSettings settings, bool isDefault)
        {
            if (isDefault)
                return $"{settings.textureCompression}{(settings.crunchedCompression ? " · Crunch" : string.Empty)} · {settings.maxTextureSize}";
            return settings.overridden ? $"{TextureFormats.Label(settings.format)} · {settings.maxTextureSize}" : "(default)";
        }

        private static TextureImporterFormat? EffectiveFormat(TextureRow row, TextureImporterPlatformSettings current, TextureImporterPlatformSettings settings, bool isDefault, string platform)
        {
            var importer = row.Importer;
            if (!isDefault)
                return settings.overridden && settings.format != TextureImporterFormat.Automatic ? settings.format : importer.GetAutomaticFormat(platform);

            var platformSettings = importer.GetPlatformTextureSettings(platform);
            if (platformSettings.overridden)
                return platformSettings.format != TextureImporterFormat.Automatic ? platformSettings.format : importer.GetAutomaticFormat(platform);
            if (settings.textureCompression == TextureImporterCompression.Uncompressed)
                return row.HasAlpha ? TextureImporterFormat.RGBA32 : TextureImporterFormat.RGB24;
            if (current.textureCompression == TextureImporterCompression.Uncompressed || settings.crunchedCompression != current.crunchedCompression)
                return null;
            return importer.GetAutomaticFormat(platform);
        }

        private static int EffectiveMaxSize(TextureRow row, TextureImporterPlatformSettings settings, bool isDefault, string platform)
        {
            if (!isDefault)
                return settings.overridden ? settings.maxTextureSize : row.Importer.maxTextureSize;
            var platformSettings = row.Importer.GetPlatformTextureSettings(platform);
            return platformSettings.overridden ? platformSettings.maxTextureSize : settings.maxTextureSize;
        }

        private static long Estimate(TextureRow row, int maxSize, TextureImporterFormat? format) =>
            format.HasValue
                ? TextureFormats.EstimateBytes(row.Width, row.Height, maxSize, row.Importer.mipmapEnabled, format.Value)
                : TextureFormats.Unknown;

        private void ApplyChanges()
        {
            var selected = _rows.Where(row => row.IsChecked).ToList();
            var pending = _plans.Values.Where(plan => plan.HasChanges).ToList();
            if (selected.Count == 0 || pending.Count == 0)
                return;

            var changes = string.Join("\n", pending.Select(plan => $"{PlatformLabel(plan.Platform)}: {string.Join(", ", plan.Describe())}"));
            var packed = selected.Count(row => row.IsPacked);
            var warning = packed > 0 ? $"\n\n{packed} of them are packed in a Sprite Atlas, so the atlas settings decide their size in build." : string.Empty;
            if (!EditorUtility.DisplayDialog("Apply Texture Compression", $"Change the import settings of {selected.Count} texture(s)?\n\n{changes}{warning}\n\nThe .meta files change; revert them with version control if needed.", "Apply", "Cancel"))
                return;

            var changed = TextureCompressionApplier.Apply(selected.Select(row => row.Path).ToList(), pending);
            _context.InvalidateInsights();
            var checkedPaths = new HashSet<string>(selected.Select(row => row.Path));
            ResetPlans();
            LoadRows();
            foreach (var row in _rows.Where(row => checkedPaths.Contains(row.Path)))
                row.IsChecked = true;
            SelectPlatform(_platform);
            EditorWindow.focusedWindow?.ShowNotification(new UnityEngine.GUIContent($"{changed} texture(s) reimported"));
        }
    }
}
