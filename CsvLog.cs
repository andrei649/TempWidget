using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace TempWidget
{
    /// <summary>
    /// Optional logging of every numeric reading to one CSV file per day in
    /// %APPDATA%\TempWidget\logs. Files older than 30 days are removed.
    /// </summary>
    internal sealed class CsvLog
    {
        public static readonly string Dir = Path.Combine(Settings.Dir, "logs");
        private string _header, _file;
        private bool _cleaned;

        public void Write(List<Row> rows)
        {
            try
            {
                FormatRows(rows, DateTime.Now, out string h, out string line);

                Directory.CreateDirectory(Dir);
                string file = Path.Combine(Dir, "TempWidget-" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".csv");
                // A new day, or a different set of sensors (a section was switched on or off): write the column names again.
                bool needHeader = file != _file || h != _header || !File.Exists(file);
                using (var w = new StreamWriter(file, true, new UTF8Encoding(false)))
                {
                    if (needHeader) w.WriteLine(h);
                    w.WriteLine(line);
                }
                _file = file;
                _header = h;
                if (!_cleaned) { _cleaned = true; RemoveOld(); }
            }
            catch { /* logging must never disturb the widget */ }
        }

        internal static void FormatRows(List<Row> rows, DateTime time, out string columnNames, out string values)
        {
            var header = new StringBuilder("Time");
            var line = new StringBuilder(time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            string section = "";
            foreach (var r in rows)
            {
                if (r.Kind == RowKind.Header) { section = r.Label; continue; }
                if (r.Key == null || !r.HasValue) continue;
                string unit = r.Kind == RowKind.Temp ? "\u00B0C" : r.Unit;
                header.Append(',').Append(Quote(section + " " + r.Label + (unit != null ? " (" + unit + ")" : "")));
                line.Append(',').Append(r.Value.ToString("0.##", CultureInfo.InvariantCulture));
            }
            columnNames = header.ToString();
            values = line.ToString();
        }

        private static string Quote(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

        private static void RemoveOld()
        {
            try
            {
                foreach (string f in Directory.GetFiles(Dir, "TempWidget-*.csv"))
                    if (File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-30)) File.Delete(f);
            }
            catch { }
        }
    }
}
