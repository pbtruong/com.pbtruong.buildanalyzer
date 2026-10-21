using System;
using System.Globalization;

namespace PBTruong.BuildAnalyzer.Editor
{
    public static class SizeFormat
    {
        private static readonly string[] Units = { "B", "KB", "MB", "GB" };

        public static string Bytes(long bytes)
        {
            double value = Math.Abs(bytes);
            var unit = 0;
            while (value >= 1024 && unit < Units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            var sign = bytes < 0 ? "-" : string.Empty;
            var number = unit == 0 ? value.ToString("0", CultureInfo.InvariantCulture) : value.ToString("0.##", CultureInfo.InvariantCulture);
            return $"{sign}{number} {Units[unit]}";
        }

        public static string Signed(long bytes) => bytes > 0 ? "+" + Bytes(bytes) : Bytes(bytes);

        public static string Percent(long part, long total) =>
            total <= 0 ? "-" : (100.0 * part / total).ToString("0.0", CultureInfo.InvariantCulture) + "%";

        public static string Raw(long bytes) => bytes.ToString(CultureInfo.InvariantCulture);
    }
}
