namespace LQAtools.Views;

public sealed class ChecklistPage : NotePageBase
{
    protected override ColumnDef[] ColumnDefs { get; } =
    [
        new("String ID", "Col1"),
        new("Source", "Col2"),
        new("Steps to Reproduce", "Col3"),
        new("French", "Col4"),
        new("Italian", "Col5"),
        new("German", "Col6"),
        new("Spanish", "Col7"),
    ];
}
