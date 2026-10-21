using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PBTruong.BuildAnalyzer.Editor
{
    public sealed class CsvTable
    {
        public CsvTable(string[] header, IReadOnlyList<string[]> rows)
        {
            Header = header;
            Rows = rows;
        }

        public string[] Header { get; }
        public IReadOnlyList<string[]> Rows { get; }
    }

    public static class CsvExporter
    {
        public static void Save(CsvTable table, string path)
        {
            var builder = new StringBuilder();
            builder.AppendLine(ToLine(table.Header));
            foreach (var row in table.Rows)
                builder.AppendLine(ToLine(row));
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
        }

        private static string ToLine(IEnumerable<string> cells) => string.Join(",", cells.Select(Escape));

        private static string Escape(string value)
        {
            value ??= string.Empty;
            return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0 ? value : $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
