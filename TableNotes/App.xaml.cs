using Microsoft.UI.Xaml;
using TableNotes.Services;

namespace TableNotes;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppDataStores.LoadConfiguration();
        var mainWindow = new MainWindow();
        mainWindow.Activate();
    }
}
