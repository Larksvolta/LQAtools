using TableNotes.Shared;

namespace TableNotes.Server.Data;

public static class RowMapping
{
    public static int ColumnCount(string page) => page switch
    {
        PageNames.Checklist => 7,
        PageNames.TextFiles => 6,
        PageNames.Changelog => 10,
        PageNames.BugTracker => 23,
        _ => 0
    };

    public static RowDto ToDto(string page, INoteRow row) => page switch
    {
        PageNames.Checklist => ChecklistToDto((ChecklistRow)row),
        PageNames.TextFiles => TextFilesToDto((TextFilesRow)row),
        PageNames.Changelog => ChangelogToDto((ChangelogRow)row),
        PageNames.BugTracker => BugTrackerToDto((BugTrackerRow)row),
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, "Unknown page")
    };

    public static INoteRow FromDto(string page, RowDto dto, Guid noteId, int position)
    {
        var id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id;
        return page switch
        {
            PageNames.Checklist => new ChecklistRow
            {
                Id = id, NoteId = noteId, Position = position, Version = 1,
                StringId = dto.Col1, Source = dto.Col2, StepsToReproduce = dto.Col3,
                French = dto.Col4, Italian = dto.Col5, German = dto.Col6, Spanish = dto.Col7
            },
            PageNames.TextFiles => new TextFilesRow
            {
                Id = id, NoteId = noteId, Position = position, Version = 1,
                StringId = dto.Col1, Source = dto.Col2, French = dto.Col3,
                Italian = dto.Col4, German = dto.Col5, Spanish = dto.Col6
            },
            PageNames.Changelog => new ChangelogRow
            {
                Id = id, NoteId = noteId, Position = position, Version = 1,
                Number = dto.Col1, Tester = dto.Col2, Date = dto.Col3, Type = dto.Col4,
                Description = dto.Col5, StringId = dto.Col6, Source = dto.Col7,
                ActualResult = dto.Col8, ExpectedResult = dto.Col9, Status = dto.Col10
            },
            PageNames.BugTracker => new BugTrackerRow
            {
                Id = id, NoteId = noteId, Position = position, Version = 1,
                Number = dto.Col1, Username = dto.Col2, Date = dto.Col3, Type = dto.Col4,
                Summary = dto.Col5, Description = dto.Col6, StepsToReproduce = dto.Col7,
                French = dto.Col8, Italian = dto.Col9, German = dto.Col10, Spanish = dto.Col11,
                BugStatus = dto.Col12, FrenchObserved = dto.Col13, FrenchExpected = dto.Col14,
                ItalianObserved = dto.Col15, ItalianExpected = dto.Col16,
                GermanObserved = dto.Col17, GermanExpected = dto.Col18,
                SpanishObserved = dto.Col19, SpanishExpected = dto.Col20,
                ObservedResult = dto.Col21, ExpectedResult = dto.Col22,
                NeedsLanguageSpecificResults = dto.Col23
            },
            _ => throw new ArgumentOutOfRangeException(nameof(page), page, "Unknown page")
        };
    }

    private static RowDto ChecklistToDto(ChecklistRow r) => new()
    {
        Id = r.Id, Position = r.Position, Version = r.Version,
        Col1 = r.StringId, Col2 = r.Source, Col3 = r.StepsToReproduce,
        Col4 = r.French, Col5 = r.Italian, Col6 = r.German, Col7 = r.Spanish
    };

    private static RowDto TextFilesToDto(TextFilesRow r) => new()
    {
        Id = r.Id, Position = r.Position, Version = r.Version,
        Col1 = r.StringId, Col2 = r.Source, Col3 = r.French,
        Col4 = r.Italian, Col5 = r.German, Col6 = r.Spanish
    };

    private static RowDto ChangelogToDto(ChangelogRow r) => new()
    {
        Id = r.Id, Position = r.Position, Version = r.Version,
        Col1 = r.Number, Col2 = r.Tester, Col3 = r.Date, Col4 = r.Type,
        Col5 = r.Description, Col6 = r.StringId, Col7 = r.Source,
        Col8 = r.ActualResult, Col9 = r.ExpectedResult, Col10 = r.Status
    };

    private static RowDto BugTrackerToDto(BugTrackerRow r) => new()
    {
        Id = r.Id, Position = r.Position, Version = r.Version,
        Col1 = r.Number, Col2 = r.Username, Col3 = r.Date, Col4 = r.Type,
        Col5 = r.Summary, Col6 = r.Description, Col7 = r.StepsToReproduce,
        Col8 = r.French, Col9 = r.Italian, Col10 = r.German, Col11 = r.Spanish,
        Col12 = r.BugStatus, Col13 = r.FrenchObserved, Col14 = r.FrenchExpected,
        Col15 = r.ItalianObserved, Col16 = r.ItalianExpected,
        Col17 = r.GermanObserved, Col18 = r.GermanExpected,
        Col19 = r.SpanishObserved, Col20 = r.SpanishExpected,
        Col21 = r.ObservedResult, Col22 = r.ExpectedResult,
        Col23 = r.NeedsLanguageSpecificResults
    };
}

public static class NoteMapping
{
    public static NoteDto ToDto(Note n) => new()
    {
        Id = n.Id,
        PageName = n.PageName,
        Title = n.Title,
        Category = n.Category,
        CreatedAt = AsOffset(n.CreatedAtUtc),
        ModifiedAt = AsOffset(n.ModifiedAtUtc),
        Version = n.Version,
        IsPlaceholder = n.IsPlaceholder
    };

    private static DateTimeOffset AsOffset(DateTime utc) =>
        new(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
}
