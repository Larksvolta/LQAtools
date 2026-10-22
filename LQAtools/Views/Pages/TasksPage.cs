namespace LQAtools.Views;

public sealed class TasksPage : NotePageBase
{
    protected override ColumnDef[] ColumnDefs { get; } =
    [
        new("String ID", "Col8"),
        new("New Source", "Col9"),
        new("Old Source", "Col10"),
        new("French", "Col11"),
        new("Italian", "Col12"),
        new("German", "Col13"),
        new("Spanish", "Col14"),
    ];
}
