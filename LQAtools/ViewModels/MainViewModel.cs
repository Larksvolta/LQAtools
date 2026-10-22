using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LQAtools.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public NotePageViewModel ChecklistVm { get; }
    public NotePageViewModel BugTrackerVm { get; }
    public NotePageViewModel ChangelogVm { get; }
    public NotePageViewModel TextFilesVm { get; }
    public NotePageViewModel GlossaryVm { get; }
    public NotePageViewModel TasksVm { get; }
    public NotePageViewModel ComplianceVm { get; }
    public TeamLeadViewModel TeamLeadVm { get; }

    public MainViewModel()
    {
        ChecklistVm = new NotePageViewModel("Checklist");
        BugTrackerVm = new NotePageViewModel("BugTracker");
        ChangelogVm = new NotePageViewModel("Changelog");
        TextFilesVm = new NotePageViewModel("TextFiles");
        GlossaryVm = new NotePageViewModel("Glossary");
        TasksVm = new NotePageViewModel("Tasks");
        ComplianceVm = new NotePageViewModel("Compliance");
        TeamLeadVm = new TeamLeadViewModel();
    }

    [RelayCommand]
    private async Task LoadAll()
    {
        await ChecklistVm.LoadNotesAsync();
        await BugTrackerVm.LoadNotesAsync();
        await ChangelogVm.LoadNotesAsync();
        await TextFilesVm.LoadNotesAsync();
        await GlossaryVm.LoadNotesAsync();
        await TasksVm.LoadNotesAsync();
        await ComplianceVm.LoadNotesAsync();
        await TeamLeadVm.LoadAsync();
    }

    public async Task ReloadAllAsync()
    {
        await ChecklistVm.ReloadStoreAsync();
        await BugTrackerVm.ReloadStoreAsync();
        await ChangelogVm.ReloadStoreAsync();
        await TextFilesVm.ReloadStoreAsync();
        await GlossaryVm.ReloadStoreAsync();
        await TasksVm.ReloadStoreAsync();
        await ComplianceVm.ReloadStoreAsync();
        await TeamLeadVm.LoadAsync();
    }
}
