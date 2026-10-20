using Microsoft.EntityFrameworkCore;
using TableNotes.Server.Api;
using TableNotes.Server.Data;
using TableNotes.Server.Import;
using TableNotes.Server.Storage;

var builder = WebApplication.CreateBuilder(args);

var contentRoot = builder.Environment.ContentRootPath;

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=Storage/tablenotes.db";
connectionString = ResolveSqlitePath(connectionString, contentRoot);

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddTableNotesAuth(builder.Configuration);
builder.Services.AddScoped<LocalDataImporter>();
builder.Services.AddSingleton<IBlobStore>(_ =>
{
    var root = builder.Configuration["Storage:BlobRoot"] ?? "Storage/blobs";
    if (!Path.IsPathRooted(root))
        root = Path.Combine(contentRoot, root);
    return new LocalBlobStore(root);
});

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 200L * 1024 * 1024);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseTableNotesAuth();

app.MapAuthApi();
app.MapNotesApi();
app.MapRowsApi();
app.MapCellMarkingApi();
app.MapScreenshotApi();

if (args.Contains("--import", StringComparer.OrdinalIgnoreCase))
{
    var dataRoot = GetArgValue(args, "--data") ?? "Data";
    if (!Path.IsPathRooted(dataRoot))
        dataRoot = Path.Combine(contentRoot, "..", dataRoot);
    using var importScope = app.Services.CreateScope();
    await importScope.ServiceProvider.GetRequiredService<LocalDataImporter>().ImportAsync(dataRoot);
    return;
}

app.MapGet("/", () => Results.Redirect("/api/health"));

app.Run();

static string? GetArgValue(string[] args, string name)
{
    var index = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    if (index >= 0 && index + 1 < args.Length)
        return args[index + 1];
    var inline = args.FirstOrDefault(a => a.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase));
    return inline?[(name.Length + 1)..];
}

static string ResolveSqlitePath(string connectionString, string contentRoot)
{
    const string prefix = "Data Source=";
    var index = connectionString.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
    if (index < 0)
        return connectionString;

    var start = index + prefix.Length;
    var end = connectionString.IndexOf(';', start);
    var path = end < 0 ? connectionString[start..] : connectionString[start..end];

    if (path.Length == 0 || path == ":memory:" || Path.IsPathRooted(path))
        return connectionString;

    var resolved = Path.GetFullPath(Path.Combine(contentRoot, path));
    Directory.CreateDirectory(Path.GetDirectoryName(resolved)!);

    return end < 0
        ? connectionString[..start] + resolved
        : connectionString[..start] + resolved + connectionString[end..];
}
