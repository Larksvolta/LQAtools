using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LQAtools.Models;
using LQAtools.Services;

namespace LQAtools.ViewModels;

public partial class TeamLeadViewModel : ObservableObject
{
    public static readonly string[] ProjectStatuses = ["Planned", "In Progress", "Done"];

    public ObservableCollection<TeamLeadUser> Users { get; } = new();
    public ObservableCollection<TeamLeadUser> WindowsUsers { get; } = new();

    [ObservableProperty]
    private string _currentUser = string.Empty;

    [ObservableProperty]
    private string _currentUserSource = string.Empty;

    [ObservableProperty]
    private string _projectName = string.Empty;

    [ObservableProperty]
    private string _projectLead = string.Empty;

    [ObservableProperty]
    private string _projectStatus = string.Empty;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _userRole = string.Empty;

    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    private TeamLeadUser? _selectedUser;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage = string.Empty;

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    public async Task LoadAsync()
    {
        var project = TeamLeadStore.LoadProject();
        ProjectName = project.Name;
        ProjectLead = project.Lead;
        ProjectStatus = project.Status;

        Users.Clear();
        foreach (var user in TeamLeadStore.LoadUsers())
            Users.Add(user);

        WindowsUsers.Clear();
        try
        {
            var store = AppDataStores.For("Checklist");
            var name = await store.GetCurrentUserAsync();

            CurrentUser = string.IsNullOrWhiteSpace(name)
                ? Environment.UserName
                : UserNames.Friendly(name);
            CurrentUserSource = AppDataStores.IsRemote
                ? $"Windows account \u00b7 {AppDataStores.ServerUrl}"
                : "Local account on this machine";

            foreach (var u in await store.GetKnownUsersAsync())
                WindowsUsers.Add(new TeamLeadUser
                {
                    Name = UserNames.Friendly(u.Name),
                    Role = "Windows account",
                    Email = u.LastSeenAt.LocalDateTime.ToString("g"),
                });
        }
        catch
        {
            CurrentUser = UserNames.Friendly(Environment.UserName);
            CurrentUserSource = "Local account on this machine";
        }
    }

    [RelayCommand]
    private void SaveProject()
    {
        TeamLeadStore.SaveProject(new TeamLeadProject
        {
            Name = ProjectName,
            Lead = ProjectLead,
            Status = ProjectStatus,
        });
        StatusMessage = "Project saved.";
    }

    [RelayCommand]
    private void AddUser()
    {
        if (string.IsNullOrWhiteSpace(UserName)) return;
        Users.Add(new TeamLeadUser
        {
            Name = UserName.Trim(),
            Role = UserRole.Trim(),
            Email = UserEmail.Trim(),
        });
        UserName = string.Empty;
        UserRole = string.Empty;
        UserEmail = string.Empty;
        TeamLeadStore.SaveUsers(Users.ToList());
        StatusMessage = "User added.";
    }

    [RelayCommand]
    private void RemoveUser()
    {
        if (SelectedUser is null) return;
        Users.Remove(SelectedUser);
        SelectedUser = null;
        TeamLeadStore.SaveUsers(Users.ToList());
        StatusMessage = "User removed.";
    }
}