namespace LQAtools.Shared;

public sealed class CellSelection
{
    public string User { get; set; } = string.Empty;
    public string NoteId { get; set; } = string.Empty;
    public int RowPosition { get; set; }
    public string ColumnProp { get; set; } = string.Empty;
}

public sealed class PresenceSnapshot
{
    public List<CellSelection> Selections { get; set; } = new();
}

public sealed class PresenceUpdate
{
    public string User { get; set; } = string.Empty;
    public string NoteId { get; set; } = string.Empty;
    public int RowPosition { get; set; }
    public string ColumnProp { get; set; } = string.Empty;
    public bool Active { get; set; }
}

public static class UserColors
{
    private const string Palette = "#2563EB,#EAB308,#16A34A,#DC2626,#9333EA,#0891B2,#EA580C,#DB2777,#4F46E5,#65A30D";

    public static readonly string[] All = Palette.Split(',');

    public static string For(string? user)
    {
        if (string.IsNullOrWhiteSpace(user))
            return All[0];

        var hash = 17;
        foreach (var c in user.Trim().ToLowerInvariant())
            hash = unchecked(hash * 31 + c);

        var index = (int)((uint)hash % (uint)All.Length);
        return All[index];
    }
}
