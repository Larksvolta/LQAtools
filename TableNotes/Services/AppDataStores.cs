namespace TableNotes.Services;

internal static class AppDataStores
{
    private static string? _serverUrl;

    public static string? ServerUrl
    {
        get => _serverUrl;
        set => _serverUrl = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static bool IsRemote => _serverUrl is not null;

    public static IDataStore For(string pageName) =>
        IsRemote
            ? new RemoteDataStore(pageName, _serverUrl!)
            : new LocalDataStore(pageName);

    public static void LoadConfiguration()
    {
        var env = Environment.GetEnvironmentVariable("TABLENOTES_SERVER");
        if (!string.IsNullOrWhiteSpace(env))
        {
            ServerUrl = env;
            return;
        }

        try
        {
            var path = ConfigPath;
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    ServerUrl = text;
            }
        }
        catch { }
    }

    public static string ConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "server.txt");

    public static void SaveConfiguration()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_serverUrl))
            {
                if (File.Exists(ConfigPath))
                    File.Delete(ConfigPath);
                return;
            }

            File.WriteAllText(ConfigPath, _serverUrl);
        }
        catch { }
    }
}
