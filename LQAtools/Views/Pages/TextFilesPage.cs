namespace LQAtools.Views;

public sealed class TextFilesPage : NotePageBase
{
    protected override ColumnDef[] ColumnDefs { get; } =
    [
        new("String ID", "Col1"),
        new("Source", "Col2"),
        new("French", "Col3"),
        new("Italian", "Col4"),
        new("German", "Col5"),
        new("Spanish", "Col6"),
    ];
}
