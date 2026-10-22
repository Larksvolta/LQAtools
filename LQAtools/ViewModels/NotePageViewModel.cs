using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ClosedXML.Excel;
using LQAtools.Models;
using LQAtools.Services;

namespace LQAtools.ViewModels;

public record EditSavedMessage(
    string Type,
    string Description,
    string StringId,
    string Source,
    string ActualResult,
    string ExpectedResult,
    Guid ChecklistNoteId,
    int RowIndex,
    string ColumnProp
);

public record RevertCellMessage(Guid NoteId, int RowIndex, string ColumnProp, bool Reapply = false, string ExpectedResult = "");
public record SetChangelogPendingMessage(Guid NoteId, int RowIndex, string ColumnProp);
public record SaveConflictMessage(string PageName);

public partial class NotePageViewModel : ObservableObject
{
    private IDataStore _dataStore;
    public IDataStore DataStore => _dataStore;

    public string PageName { get; }

    public bool ShowTreeView => PageName == "Checklist";
    public bool IsChangelog => PageName == "Changelog";
    public bool IsTextFiles => PageName == "TextFiles";
    public bool IsBugTracker => PageName == "BugTracker";
    public bool IsTasks => PageName == "Tasks";

    public static string GetChangelogNoteTitle(string columnProp) => columnProp switch
    {
        "Col2" => "Source",
        "Col4" => "French",
        "Col5" => "Italian",
        "Col6" => "German",
        "Col7" => "Spanish",
        _ => "Other",
    };

    public static readonly string[] ChangelogNoteTitles = ["Source", "French", "Italian", "German", "Spanish"];

    public ObservableCollection<TableNote> Notes { get; } = new();
    public ObservableCollection<TableRow> Rows { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedNote))]
    [NotifyPropertyChangedFor(nameof(SelectedNoteModifiedAt))]
    private TableNote? _selectedNote;

    [ObservableProperty]
    private string _editTitle = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage = string.Empty;

    public bool HasSelectedNote => SelectedNote is not null;
    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);
    public string SelectedNoteModifiedAt => SelectedNote?.ModifiedAt.ToString("g") ?? string.Empty;

    public void SetStatusMessage(string message) => StatusMessage = message;

    public NotePageViewModel(string pageName)
    {
        PageName = pageName;
        _dataStore = AppDataStores.For(pageName);
    }

    partial void OnSelectedNoteChanged(TableNote? value)
    {
        if (value is not null)
        {
            EditTitle = value.Title;
            _ = LoadRowsAsync(value);
        }
    }

    public async Task LoadNotesAsync()
    {
        var notes = await _dataStore.LoadNotesAsync();

        Notes.Clear();
        foreach (var n in notes)
            Notes.Add(n);

        if (Notes.Count == 0 && PageName == "Tasks")
        {
            var note = await _dataStore.CreateNoteAsync("Tasks");
            Notes.Add(note);
        }

        if (Notes.Count > 0 && SelectedNote is null)
            SelectedNote = Notes[0];
    }

    private async Task LoadRowsAsync(TableNote note)
    {
        Rows.Clear();
        var rows = await _dataStore.LoadRowsAsync(note);
        foreach (var r in rows)
            Rows.Add(r);
    }

    public async Task ReloadSelectedRowsAsync()
    {
        if (SelectedNote is null)
            return;
        await LoadRowsAsync(SelectedNote);
    }

    public async Task ReloadStoreAsync()
    {
        _dataStore = AppDataStores.For(PageName);
        SelectedNote = null;
        Notes.Clear();
        Rows.Clear();
        await LoadNotesAsync();
    }

    public async Task AddChangelogEntry(string type, string description, string stringId, string source, string actualResult, string expectedResult, string columnProp)
    {
        var noteTitle = GetChangelogNoteTitle(columnProp);
        var note = Notes.FirstOrDefault(n => n.Title == noteTitle);
        if (note is null)
        {
            note = await _dataStore.CreateNoteAsync(noteTitle);
            Notes.Insert(0, note);
        }

        var rows = await _dataStore.LoadRowsAsync(note);
        var nextId = (rows.Count + 1).ToString();
        var row = new TableRow
        {
            Col1 = nextId,
            Col2 = Environment.UserName,
            Col3 = DateTime.Now.ToString("yyyy/MM/dd"),
            Col4 = type,
            Col5 = description,
            Col6 = stringId,
            Col7 = source,
            Col8 = actualResult,
            Col9 = expectedResult,
            Col10 = "Pending",
        };
        rows.Add(row);
        try
        {
            await _dataStore.SaveRowsAsync(note, rows);
        }
        catch (ConcurrencyConflictException)
        {
            StatusMessage = "Conflict: this note was changed elsewhere.";
            WeakReferenceMessenger.Default.Send(new SaveConflictMessage(PageName));
            return;
        }

        if (SelectedNote == note)
        {
            Rows.Clear();
            foreach (var r in rows)
                Rows.Add(r);
        }
    }

    [RelayCommand]
    private async Task NewNote()
    {
        if (PageName is "Changelog" or "TextFiles" or "BugTracker" or "Tasks") return;
        var note = await _dataStore.CreateNoteAsync("New Note");
        Notes.Insert(0, note);
        SelectedNote = note;
        Rows.Clear();
        Rows.Add(new TableRow());
        StatusMessage = "New note created";
    }

    [RelayCommand]
    private async Task SaveNote()
    {
        try
        {
            if (SelectedNote is null)
            {
                var note = await _dataStore.CreateNoteAsync(string.IsNullOrWhiteSpace(EditTitle) ? "New Note" : EditTitle);
                Notes.Insert(0, note);
                SelectedNote = note;
            }
            else
            {
                await _dataStore.SaveNoteAsync(SelectedNote, EditTitle);
            }

            await _dataStore.SaveRowsAsync(SelectedNote!, Rows.ToList());
            StatusMessage = "Note saved";
        }
        catch (ConcurrencyConflictException)
        {
            StatusMessage = "Conflict: this note was changed elsewhere.";
            WeakReferenceMessenger.Default.Send(new SaveConflictMessage(PageName));
        }
    }

    [RelayCommand]
    private async Task DeleteNote()
    {
        if (PageName is "Changelog" or "TextFiles" or "BugTracker" or "Tasks") return;
        if (SelectedNote is null) return;

        var toRemove = SelectedNote;
        var idx = Notes.IndexOf(toRemove);

        SelectedNote = null;
        EditTitle = string.Empty;
        Rows.Clear();
        Notes.Remove(toRemove);
        await _dataStore.DeleteNoteAsync(toRemove);

        if (Notes.Count > 0)
        {
            var nextIdx = Math.Min(idx, Notes.Count - 1);
            SelectedNote = Notes[nextIdx];
        }

        StatusMessage = "Note deleted";
    }

    [RelayCommand]
    private void AddRow()
    {
        var row = new TableRow();
        if (PageName is "BugTracker" or "Tasks")
        {
            row.Col1 = (Rows.Count + 1).ToString();
            row.Col2 = Environment.UserName;
            row.Col3 = DateTime.Now.ToString("yyyy/MM/dd");
        }
        if (PageName is "BugTracker" or "Tasks")
            SelectedRowIndex = Rows.Count;

        Rows.Add(row);
    }

    private int _selectedRowIndex = -1;

    public int SelectedRowIndex
    {
        get => _selectedRowIndex;
        set
        {
            if (_selectedRowIndex != value)
            {
                _selectedRowIndex = value;
                OnPropertyChanged();
            }
        }
    }
}
