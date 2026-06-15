namespace Portal.Models;

public sealed record ReportDef(
    string   Id,
    string   Title,
    string   Group,
    string   Description,
    bool     HasDateFilter = true,
    bool     HasLocationFilter = true
);

public sealed class ReportResult
{
    public string[]        Columns { get; init; } = [];
    public List<object?[]> Rows    { get; init; } = [];
    public int             Total   => Rows.Count;
}
