using ClosedXML.Excel;

namespace Portal.Services;

public static class ImportHelper
{
    public static string[] ParseLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var sb     = new System.Text.StringBuilder();
        bool inQ   = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"') { if (inQ && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else inQ = !inQ; }
            else if (c == delimiter && !inQ) { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        fields.Add(sb.ToString());
        return [.. fields];
    }

    public static Dictionary<string, int> BuildHeaderMap(IXLWorksheet ws)
    {
        var map     = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (int c = 1; c <= lastCol; c++)
        {
            var h = ws.Cell(1, c).GetString().Trim();
            if (!string.IsNullOrEmpty(h)) map[h] = c;
        }
        return map;
    }

    public static async IAsyncEnumerable<Dictionary<string, string>> ExcelRowsAsync(Stream ms)
    {
        using var wb  = new XLWorkbook(ms);
        var ws        = wb.Worksheets.First();
        var headers   = BuildHeaderMap(ws);
        int lastRow   = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (int row = 2; row <= lastRow; row++)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, col) in headers)
                d[key] = ws.Cell(row, col).GetString().Trim();
            yield return d;
            await Task.CompletedTask;
        }
    }

    public static async IAsyncEnumerable<Dictionary<string, string>> TextRowsAsync(
        Stream ms, char delimiter,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        using var rdr  = new StreamReader(ms, detectEncodingFromByteOrderMarks: true);
        var headerLine = await rdr.ReadLineAsync(ct);
        if (headerLine is null) yield break;
        var headers = ParseLine(headerLine, delimiter)
            .Select((h, i) => (h.Trim(), i))
            .Where(x => !string.IsNullOrEmpty(x.Item1))
            .ToDictionary(x => x.Item1, x => x.i, StringComparer.OrdinalIgnoreCase);
        string? line;
        while ((line = await rdr.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = ParseLine(line, delimiter);
            var d    = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, idx) in headers)
                d[key] = idx < cols.Length ? cols[idx].Trim() : string.Empty;
            yield return d;
        }
    }

    public static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
