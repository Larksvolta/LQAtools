namespace LQAtools.Services;

internal static class AppDataStores
{
    private static string? _serverUrl;

    public static string? ServerUrl
    {
        get => _serverUrl;
        set => _serverUrl = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static bool IsRemote => _serverUrl is not null;

    public static Uri ResolveUri(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"'{baseUrl}' is not a valid server URL. Use a full http(s) URL such as https://lqatools.example.com.");
        }

        if (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback
            && Environment.GetEnvironmentVariable("LQATOOLS_ALLOW_INSECURE") != "1")
        {
            throw new InvalidOperationException(
                "Refusing to send Windows credentials to a non-local server over plain HTTP. " +
                "Use an https:// URL, or set LQATOOLS_ALLOW_INSECURE=1 only on a trusted network.");
        }

        return uri;
    }

    public static IDataStore For(string pageName) =>
        IsRemote
            ? new RemoteDataStore(pageName, _serverUrl!)
            : new LocalDataStore(pageName);

    public static void LoadConfiguration()
    {
        var env = Environment.GetEnvironmentVariable("LQATOOLS_SERVER");
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
