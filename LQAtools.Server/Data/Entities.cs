namespace LQAtools.Server.Data;

public sealed class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PageName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;
    public bool IsPlaceholder { get; set; }
    public string? CreatedBy { get; set; }
    public string? LastModifiedBy { get; set; }
}

public sealed class AppUser
{
    public string Name { get; set; } = string.Empty;
    public DateTime FirstSeenAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
}

public interface INoteRow
{
    Guid Id { get; set; }
    Guid NoteId { get; set; }
    int Position { get; set; }
    int Version { get; set; }
}

public sealed class ChecklistRow : INoteRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NoteId { get; set; }
    public int Position { get; set; }
    public int Version { get; set; } = 1;

    public string StringId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string StepsToReproduce { get; set; } = string.Empty;
    public string French { get; set; } = string.Empty;
    public string Italian { get; set; } = string.Empty;
    public string German { get; set; } = string.Empty;
    public string Spanish { get; set; } = string.Empty;
}

public sealed class TextFilesRow : INoteRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NoteId { get; set; }
    public int Position { get; set; }
    public int Version { get; set; } = 1;

    public string StringId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string French { get; set; } = string.Empty;
    public string Italian { get; set; } = string.Empty;
    public string German { get; set; } = string.Empty;
    public string Spanish { get; set; } = string.Empty;
}

public sealed class ChangelogRow : INoteRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NoteId { get; set; }
    public int Position { get; set; }
    public int Version { get; set; } = 1;

    public string Number { get; set; } = string.Empty;
    public string Tester { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StringId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string ActualResult { get; set; } = string.Empty;
    public string ExpectedResult { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class BugTrackerRow : INoteRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NoteId { get; set; }
    public int Position { get; set; }
    public int Version { get; set; } = 1;

    public string Number { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StepsToReproduce { get; set; } = string.Empty;
    public string French { get; set; } = string.Empty;
    public string Italian { get; set; } = string.Empty;
    public string German { get; set; } = string.Empty;
    public string Spanish { get; set; } = string.Empty;
    public string BugStatus { get; set; } = string.Empty;
    public string FrenchObserved { get; set; } = string.Empty;
    public string FrenchExpected { get; set; } = string.Empty;
    public string ItalianObserved { get; set; } = string.Empty;
    public string ItalianExpected { get; set; } = string.Empty;
    public string GermanObserved { get; set; } = string.Empty;
    public string GermanExpected { get; set; } = string.Empty;
    public string SpanishObserved { get; set; } = string.Empty;
    public string SpanishExpected { get; set; } = string.Empty;
    public string ObservedResult { get; set; } = string.Empty;
    public string ExpectedResult { get; set; } = string.Empty;
    public string NeedsLanguageSpecificResults { get; set; } = string.Empty;
}

public sealed class GenericRow : INoteRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NoteId { get; set; }
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

public sealed class CellMarking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NoteId { get; set; }
    public int RowPosition { get; set; }
    public string ColumnProp { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Screenshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NoteId { get; set; }
    public string BugId { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string BlobKey { get; set; } = string.Empty;
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAtUtc { get; set; }
}
