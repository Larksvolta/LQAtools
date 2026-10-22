namespace LQAtools.Views;

public sealed class BugTrackerPage : NotePageBase
{
    protected override ColumnDef[] ColumnDefs { get; } =
    [
        new("#", "Col1"),
        new("Username", "Col2"),
        new("Date", "Col3"),
        new("Type", "Col4"),
        new("Summary", "Col5"),
        new("Description", "Col6"),
        new("Steps to Reproduce", "Col7"),
        new("French", "Col8"),
        new("Italian", "Col9"),
        new("German", "Col10"),
        new("Spanish", "Col11"),
        new("Bug Status", "Col12"),
    ];
}
