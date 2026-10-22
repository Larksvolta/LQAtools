namespace LQAtools.Views;

public sealed class GlossaryPage : NotePageBase
{
    protected override ColumnDef[] ColumnDefs { get; } =
    [
        new("Term", "Col1"),
        new("French", "Col2"),
        new("Italian", "Col3"),
        new("German", "Col4"),
        new("Spanish", "Col5"),
    ];
}
