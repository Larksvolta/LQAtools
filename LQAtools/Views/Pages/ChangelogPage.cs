namespace LQAtools.Views;

public sealed class ChangelogPage : NotePageBase
{
    protected override ColumnDef[] ColumnDefs { get; } =
    [
        new("#", "Col1"),
        new("Tester", "Col2"),
        new("Date", "Col3"),
        new("Type", "Col4"),
        new("Description", "Col5"),
        new("String ID", "Col6"),
        new("Source", "Col7"),
        new("Actual Result", "Col8"),
        new("Expected Result", "Col9"),
        new("Status", "Col10"),
    ];
}
