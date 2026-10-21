using UnityEngine.UIElements;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal interface IAnalyzerTab
    {
        string Title { get; }
        VisualElement Root { get; }
        bool CanExport { get; }
        void Refresh();
        CsvTable ExportCsv();
    }
}
