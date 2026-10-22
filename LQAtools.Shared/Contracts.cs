namespace LQAtools.Shared;

public static class PageNames
{
    public const string Checklist = "Checklist";
    public const string BugTracker = "BugTracker";
    public const string Changelog = "Changelog";
    public const string TextFiles = "TextFiles";
    public const string Glossary = "Glossary";
    public const string Tasks = "Tasks";
    public const string Compliance = "Compliance";

    public static readonly string[] All =
    {
        Checklist, BugTracker, Changelog, TextFiles, Glossary, Tasks, Compliance
    };

    public static bool IsValid(string? page) =>
        page is not null && Array.IndexOf(All, page) >= 0;
}

public sealed class NoteDto
{
    public Guid Id { get; set; }
    public string PageName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ModifiedAt { get; set; }
    public int Version { get; set; }
    public bool IsPlaceholder { get; set; }
    public string? CreatedBy { get; set; }
    public string? LastModifiedBy { get; set; }
}

public sealed class UserDto
{
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}

public sealed class MeResponse
{
    public string? Name { get; set; }
    public bool Authenticated { get; set; }
}

public sealed class RowDto
{
    public Guid Id { get; set; }
    public int Position { get; set; }
    public int Version { get; set; } = 1;

    public string Col1 { get; set; } = string.Empty;
    public string Col2 { get; set; } = string.Empty;
    public string Col3 { get; set; } = string.Empty;
    public string Col4 { get; set; } = string.Empty;
    public string Col5 { get; set; } = string.Empty;
    public string Col6 { get; set; } = string.Empty;
    public string Col7 { get; set; } = string.Empty;
    public string Col8 { get; set; } = string.Empty;
    public string Col9 { get; set; } = string.Empty;
    public string Col10 { get; set; } = string.Empty;
    public string Col11 { get; set; } = string.Empty;
    public string Col12 { get; set; } = string.Empty;
    public string Col13 { get; set; } = string.Empty;
    public string Col14 { get; set; } = string.Empty;
    public string Col15 { get; set; } = string.Empty;
    public string Col16 { get; set; } = string.Empty;
    public string Col17 { get; set; } = string.Empty;
    public string Col18 { get; set; } = string.Empty;
    public string Col19 { get; set; } = string.Empty;
    public string Col20 { get; set; } = string.Empty;
    public string Col21 { get; set; } = string.Empty;
    public string Col22 { get; set; } = string.Empty;
    public string Col23 { get; set; } = string.Empty;
}

public sealed class RowsResponse
{
    public int Version { get; set; }
    public List<RowDto> Rows { get; set; } = new();
}

public sealed class CellMarkingDto
{
    public Guid NoteId { get; set; }
    public int RowPosition { get; set; }
    public string ColumnProp { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
}

public sealed class ScreenshotDto
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public string BugId { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; }
}

public sealed record CreateNoteRequest(string Title, string? Category = null);

public sealed record UpdateNoteRequest(string Title, string? Category, int BaseVersion);

public sealed record SaveRowsRequest(int BaseVersion, List<RowDto> Rows);
