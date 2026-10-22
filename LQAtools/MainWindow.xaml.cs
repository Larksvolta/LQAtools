using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using LQAtools.Services;
using LQAtools.ViewModels;
using LQAtools.Views;
using Windows.Foundation;
using Windows.Graphics;
using WinRT.Interop;

namespace LQAtools;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();
    private InputNonClientPointerSource? _nonClientInput;
    private readonly TabView _tabView = new() { IsAddTabButtonVisible = false };
    private readonly NotePageBase[] _pages = new NotePageBase[7];
    private TeamLeadPage? _teamLeadPage;
    private readonly MenuFlyoutSubItem _columnVisibilityItem = new() { Text = "Column Visibility" };
    private readonly MenuFlyoutItem _userItem = new() { IsEnabled = false };
    private readonly MenuFlyoutItem _modeItem = new() { IsEnabled = false };

    public static MainWindow? Instance { get; private set; }
    public NotePageBase? TextFilesPage => _pages.FirstOrDefault(p => p.DataContext is NotePageViewModel vm && vm.PageName == "TextFiles");

    public MainWindow()
    {
        Instance = this;
        InitializeComponent();

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        _nonClientInput = InputNonClientPointerSource.GetForWindowId(windowId);

        var icon = new TextBlock
        {
            Text = "\U0001F4CB",
            FontSize = 16,
            Margin = new Thickness(12, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _tabView.TabStripHeader = icon;

        var spacer = new Grid { Width = 188 };
        _tabView.TabStripFooter = spacer;

        _tabView.Loaded += (_, _) => UpdateDragRegions();
        _tabView.SizeChanged += (_, _) => UpdateDragRegions();

        var pageNames = new[] { "Checklist", "Bug Tracker", "Changelog", "Text Files", "Glossary", "Tasks", "Compliance" };
        var viewModels = new[]
        {
            ViewModel.ChecklistVm, ViewModel.BugTrackerVm, ViewModel.ChangelogVm,
            ViewModel.TextFilesVm, ViewModel.GlossaryVm, ViewModel.TasksVm, ViewModel.ComplianceVm,
        };
        NotePageBase[] pages =
        [
            new ChecklistPage(),
            new BugTrackerPage(),
            new ChangelogPage(),
            new TextFilesPage(),
            new GlossaryPage(),
            new TasksPage(),
            new CompliancePage(),
        ];

        for (int i = 0; i < pages.Length; i++)
        {
            var page = pages[i];
            page.DataContext = viewModels[i];
            var item = new TabViewItem { Header = pageNames[i], IsClosable = false, Tag = page };
            _tabView.TabItems.Add(item);
            _pages[i] = page;
        }

        _teamLeadPage = new TeamLeadPage { DataContext = ViewModel.TeamLeadVm };
        var teamLeadItem = new TabViewItem { Header = "Team Lead", IsClosable = false, Tag = _teamLeadPage };
        _tabView.TabItems.Add(teamLeadItem);
        _teamLeadPage.Visibility = Visibility.Collapsed;
        Grid.SetRow(_teamLeadPage, 2);
        RootGrid.Children.Add(_teamLeadPage);

        _tabView.SelectionChanged += OnTabSelectionChanged;

        var menuBar = new MenuBar();

        var fileMenu = new MenuBarItem { Title = "File" };
        fileMenu.Items.Add(new MenuFlyoutItem { Text = "New Note" });
        fileMenu.Items.Add(new MenuFlyoutSeparator());
        var exitItem = new MenuFlyoutItem { Text = "Exit" };
        exitItem.Click += (_, _) => this.Close();
        fileMenu.Items.Add(exitItem);
        menuBar.Items.Add(fileMenu);

        var editMenu = new MenuBarItem { Title = "Edit" };
        var addRowItem = new MenuFlyoutItem { Text = "Add Row" };
        addRowItem.Click += (_, _) => GetCurrentVm()?.AddRowCommand.Execute(null);
        editMenu.Items.Add(addRowItem);
        var deleteNoteItem = new MenuFlyoutItem { Text = "Delete Note" };
        deleteNoteItem.Click += (_, _) => GetCurrentVm()?.DeleteNoteCommand.Execute(null);
        editMenu.Items.Add(deleteNoteItem);
        menuBar.Items.Add(editMenu);

        var viewMenu = new MenuBarItem { Title = "View" };
        viewMenu.Items.Add(new MenuFlyoutItem { Text = "Zoom In" });
        viewMenu.Items.Add(new MenuFlyoutItem { Text = "Zoom Out" });
        viewMenu.Items.Add(new MenuFlyoutSeparator());
        viewMenu.Items.Add(_columnVisibilityItem);
        menuBar.Items.Add(viewMenu);

        var accountButton = new Button
        {
            Content = new FontIcon { Glyph = "\uE77B", FontSize = 16 },
            Margin = new Thickness(0, 4, 8, 4),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var accountMenu = new MenuFlyout();
        accountMenu.Items.Add(_userItem);
        accountMenu.Items.Add(_modeItem);
        accountMenu.Items.Add(new MenuFlyoutSeparator());
        var settingsItem = new MenuFlyoutItem { Text = "Settings" };
        settingsItem.Click += async (_, _) => await ShowSettingsDialog();
        accountMenu.Items.Add(settingsItem);
        accountMenu.Opening += async (_, _) => await RefreshIdentityAsync();
        accountButton.Flyout = accountMenu;

        var topRow = new Grid();
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(menuBar, 0);
        Grid.SetColumn(accountButton, 1);
        topRow.Children.Add(menuBar);
        topRow.Children.Add(accountButton);

        Grid.SetRow(_tabView, 0);
        Grid.SetRow(topRow, 1);

        RootGrid.Children.Add(_tabView);
        RootGrid.Children.Add(topRow);

        _pages[0].Visibility = Visibility.Visible;
        Grid.SetRow(_pages[0], 2);
        RootGrid.Children.Add(_pages[0]);

        for (int i = 1; i < _pages.Length; i++)
        {
            _pages[i].Visibility = Visibility.Collapsed;
            Grid.SetRow(_pages[i], 2);
            RootGrid.Children.Add(_pages[i]);
        }

        _ = ViewModel.LoadAllCommand.ExecuteAsync(null);
        RefreshColumnVisibilityMenu();
        _ = RefreshIdentityAsync();
    }

    private async Task RefreshIdentityAsync()
    {
        try
        {
            var store = AppDataStores.For("Checklist");
            var name = await store.GetCurrentUserAsync();
            _userItem.Text = string.IsNullOrWhiteSpace(name)
                ? $"Signed in: {UserNames.Friendly(Environment.UserName)}"
                : $"Signed in: {UserNames.Friendly(name)}";
            _modeItem.Text = AppDataStores.IsRemote
                ? $"Server: {AppDataStores.ServerUrl}"
                : "Mode: local files";
        }
        catch
        {
            _userItem.Text = $"Signed in: {UserNames.Friendly(Environment.UserName)}";
            _modeItem.Text = "Mode: server unreachable";
        }
    }

    private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedTab = (_tabView.SelectedItem as TabViewItem)?.Tag as FrameworkElement;
        foreach (var page in _pages)
            page.Visibility = ReferenceEquals(page, selectedTab) ? Visibility.Visible : Visibility.Collapsed;
        if (_teamLeadPage is not null)
            _teamLeadPage.Visibility = ReferenceEquals(_teamLeadPage, selectedTab) ? Visibility.Visible : Visibility.Collapsed;
        RefreshColumnVisibilityMenu();
    }

    private FrameworkElement? GetCurrentPage() => (_tabView.SelectedItem as TabViewItem)?.Tag as FrameworkElement;

    private void RefreshColumnVisibilityMenu()
    {
        _columnVisibilityItem.Items.Clear();

        var bugPage = _pages.FirstOrDefault(p => p.DataContext is NotePageViewModel vm && vm.PageName == "BugTracker");
        if (bugPage is null || !ReferenceEquals(GetCurrentPage(), bugPage))
        {
            _columnVisibilityItem.Items.Add(new MenuFlyoutItem { Text = "Not available on this tab", IsEnabled = false });
            return;
        }

        var groups = bugPage.BugColumnGroups;
        for (int i = 0; i < groups.Length; i++)
        {
            var index = i;
            var item = new MenuFlyoutItem
            {
                Text = groups[i],
                Icon = bugPage.GetBugColumnGroupVisible(index) ? CheckIcon() : null,
            };
            item.Click += (_, _) =>
            {
                var visible = !bugPage.GetBugColumnGroupVisible(index);
                bugPage.SetBugColumnGroupVisible(index, visible);
                item.Icon = visible ? CheckIcon() : null;
            };
            _columnVisibilityItem.Items.Add(item);
        }
    }

    private static FontIcon CheckIcon() => new() { Glyph = "\uE73E" };

    private NotePageViewModel? GetCurrentVm()
    {
        var page = (_tabView.SelectedItem as TabViewItem)?.Tag as NotePageBase;
        return page?.DataContext as NotePageViewModel;
    }

    private async Task ShowSettingsDialog()
    {
        var modeText = new TextBlock
        {
            Text = AppDataStores.IsRemote
                ? $"Current mode: Shared server ({AppDataStores.ServerUrl})"
                : "Current mode: Local files (standalone)",
            TextWrapping = TextWrapping.Wrap,
        };

        var serverBox = new TextBox
        {
            Header = "Server URL",
            PlaceholderText = "e.g. http://localhost:5217",
            Text = AppDataStores.ServerUrl ?? string.Empty,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var hint = new TextBlock
        {
            Text = "Leave empty to use local files on this machine.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
        };

        var panel = new StackPanel { Spacing = 10, MinWidth = 360 };
        panel.Children.Add(modeText);
        panel.Children.Add(serverBox);
        panel.Children.Add(hint);

        var dialog = new ContentDialog
        {
            Title = "Settings",
            Content = panel,
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Use Local",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootGrid.XamlRoot,
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Secondary)
        {
            AppDataStores.ServerUrl = null;
            AppDataStores.SaveConfiguration();
            await ReloadAfterSettingsChange();
            return;
        }

        if (result != ContentDialogResult.Primary)
            return;

        var text = serverBox.Text.Trim();
        if (text.Length > 0)
        {
            try
            {
                AppDataStores.ResolveUri(text);
            }
            catch (Exception ex)
            {
                await ShowMessageDialog("Invalid server URL", ex.Message);
                return;
            }
        }

        AppDataStores.ServerUrl = text.Length == 0 ? null : text;
        AppDataStores.SaveConfiguration();
        await ReloadAfterSettingsChange();
    }

    private async Task ReloadAfterSettingsChange()
    {
        try
        {
            await ViewModel.ReloadAllAsync();
            RefreshColumnVisibilityMenu();
            await RefreshIdentityAsync();
        }
        catch (Exception ex)
        {
            await ShowMessageDialog("Could not load data",
                AppDataStores.IsRemote
                    ? $"Failed to reach the server: {ex.Message}"
                    : $"Failed to load local data: {ex.Message}");
        }
    }

    private async Task ShowMessageDialog(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "OK",
            XamlRoot = RootGrid.XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private void UpdateDragRegions()
    {
        if (_nonClientInput is null) return;
        var w = _tabView.ActualWidth;
        if (w <= 0) return;

        var topHeight = 40;
        var captionButtonsWidth = 188;
        var captionRects = new List<RectInt32>();

        var tabPositions = new List<(double left, double right)>();
        for (int i = 0; i < _tabView.TabItems.Count; i++)
        {
            if (_tabView.ContainerFromIndex(i) is TabViewItem item)
            {
                var transform = item.TransformToVisual(null);
                var pos = transform.TransformPoint(new Point(0, 0));
                tabPositions.Add((pos.X, pos.X + item.ActualWidth));
            }
        }

        if (tabPositions.Count >= 2)
        {
            captionRects.Add(new RectInt32(0, 0, (int)tabPositions[0].left, topHeight));

            for (int i = 0; i < tabPositions.Count - 1; i++)
            {
                var gapStart = (int)tabPositions[i].right;
                var gapEnd = (int)tabPositions[i + 1].left;
                var gapWidth = gapEnd - gapStart;
                if (gapWidth > 0)
                    captionRects.Add(new RectInt32(gapStart, 0, gapWidth, topHeight));
            }

            var rightStart = (int)tabPositions[^1].right;
            var rightWidth = (int)w - rightStart - captionButtonsWidth;
            if (rightWidth > 0)
                captionRects.Add(new RectInt32(rightStart, 0, rightWidth, topHeight));
        }
        else
        {
            captionRects.Add(new RectInt32(0, 0, 60, topHeight));
            var rw = (int)w - captionButtonsWidth;
            if (rw > 60)
                captionRects.Add(new RectInt32(rw, 0, captionButtonsWidth, topHeight));
        }

        _nonClientInput.SetRegionRects(NonClientRegionKind.Caption, captionRects.ToArray());
    }
}
