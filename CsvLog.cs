using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace TempWidget
{
    /// <summary>One schema per daily v2 CSV file; existing legacy logs are never appended to.</summary>
    internal sealed class CsvLog
    {
        public static readonly string Dir = Path.Combine(Settings.Dir, "logs");
        private readonly string _directory;
        private readonly Func<DateTime> _now;
        private bool _cleaned;
        private static readonly Regex OwnedFile = new Regex(@"^TempWidget-v2-\d{4}-\d{2}-\d{2}-[a-f0-9]{64}\.csv$", RegexOptions.CultureInvariant);

        public CsvLog() : this(Dir, () => DateTime.Now) { }
        internal CsvLog(string directory, Func<DateTime> now) { _directory = directory; _now = now; }

        public void Write(List<Row> rows)
        {
            try
            {
                DateTime time = _now(); // same instant for both filename and row, including midnight
                FormatRows(rows, time, out string header, out string line);
                if (header == "Time") return;
                Directory.CreateDirectory(_directory);
                string file = Path.Combine(_directory, SchemaFileName(time, header));
                using (var stream = new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                {
                    bool empty = stream.Length == 0;
                    if (!empty)
                    {
                        // Verify the on-disk schema after every restart and refuse partial/tampered
                        // files. Do not repair, truncate or overwrite a user's existing content.
                        foreach (byte expected in Encoding.UTF8.GetBytes(header + Environment.NewLine))
                            if (stream.ReadByte() != expected) return;
                        stream.Seek(-1, SeekOrigin.End);
                        if (stream.ReadByte() != '\n') return;
                    }
                    stream.Seek(0, SeekOrigin.End);
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true))
                    {
                        if (empty) writer.WriteLine(header);
                        writer.WriteLine(line);
                    }
                }
                if (!_cleaned) { _cleaned = true; RemoveOld(); }
            }
            catch { /* optional logging must never disturb the widget */ }
        }

        internal static string SchemaFileName(DateTime time, string header)
        {
            using (var hash = SHA256.Create())
            {
                string schema = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(header))).Replace("-", "").ToLowerInvariant();
                return "TempWidget-v2-" + time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "-" + schema + ".csv";
            }
        }

        internal static void FormatRows(List<Row> rows, DateTime time, out string columnNames, out string values)
        {
            // Preserve the device context before sorting. A replacement CPU/GPU at the
            // same display index must not silently inherit the previous model's schema.
            var contexts = new Dictionary<Row, string>();
            string context = "";
            foreach (var row in rows)
            {
                if (row.Kind == RowKind.Header)
                    context = row.Label + (string.IsNullOrEmpty(row.Text) ? "" : ": " + row.Text);
                else contexts[row] = context;
            }
            var columns = rows.Where(r => r.Key != null && (r.Kind == RowKind.Temp || r.Unit != null || r.HasValue))
                .OrderBy(r => r.Key, StringComparer.Ordinal).ToList();
            if (columns.GroupBy(r => r.Key, StringComparer.Ordinal).Any(g => g.Count() > 1))
                throw new ArgumentException("Duplicate sensor key in CSV sample.");
            var header = new StringBuilder("Time");
            var line = new StringBuilder(time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            foreach (var row in columns)
            {
                string unit = row.Kind == RowKind.Temp ? "\u00B0C" : row.Unit;
                // Key first: two identically named devices cannot silently share a column.
                string name = row.Key + " | " + row.Section + " | " + row.Label + (unit != null ? " (" + unit + ")" : "");
                if (contexts.TryGetValue(row, out string device) && device.Length > 0) name += " | " + device;
                name = name.Replace('\r', ' ').Replace('\n', ' ');
                header.Append(',').Append(Quote(name));
                line.Append(',');
                if (row.HasValue) line.Append(row.Value.ToString("0.##", CultureInfo.InvariantCulture));
            }
            columnNames = header.ToString();
            values = line.ToString();
        }

        private static string Quote(string value)
        {
            // Quoting CSV syntax alone does not neutralize spreadsheet formulas.
            string trimmed = value.TrimStart();
            if (trimmed.Length > 0 && "=+-@".IndexOf(trimmed[0]) >= 0) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private void RemoveOld()
        {
            try
            {
                foreach (string file in Directory.GetFiles(_directory, "TempWidget-v2-*.csv"))
                    if (OwnedFile.IsMatch(Path.GetFileName(file)) && File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-30))
                        File.Delete(file);
            }
            catch { }
        }
    }
}
