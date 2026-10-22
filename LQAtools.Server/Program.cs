using Microsoft.EntityFrameworkCore;
using LQAtools.Server.Api;
using LQAtools.Server.Data;
using LQAtools.Server.Import;
using LQAtools.Server.Storage;

var builder = WebApplication.CreateBuilder(args);

var contentRoot = builder.Environment.ContentRootPath;

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=Storage/lqatools.db";
connectionString = ResolveSqlitePath(connectionString, contentRoot);

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddLQAtoolsAuth(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddSingleton<LQAtools.Server.Api.PresenceRegistry>();
builder.Services.AddScoped<LocalDataImporter>();
builder.Services.AddSingleton<IBlobStore>(_ =>
{
    var root = builder.Configuration["Storage:BlobRoot"] ?? "Storage/blobs";
    if (!Path.IsPathRooted(root))
        root = Path.Combine(contentRoot, root);
    return new LocalBlobStore(root);
});

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 64L * 1024 * 1024);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    EnsureGenericRowsTable(db);
}

app.UseLQAtoolsHardening();
app.UseLQAtoolsAuth();

app.MapAuthApi();
app.MapNotesApi();
app.MapRowsApi();
app.MapCellMarkingApi();
app.MapScreenshotApi();
app.MapPresenceApi();

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

static void EnsureGenericRowsTable(AppDbContext db)
{
    const string createTable = """
        CREATE TABLE IF NOT EXISTS "GenericRows" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_GenericRows" PRIMARY KEY,
            "NoteId" TEXT NOT NULL,
            "Position" INTEGER NOT NULL,
            "Version" INTEGER NOT NULL,
            "Col1" TEXT NOT NULL,
            "Col2" TEXT NOT NULL,
            "Col3" TEXT NOT NULL,
            "Col4" TEXT NOT NULL,
            "Col5" TEXT NOT NULL,
            "Col6" TEXT NOT NULL,
            "Col7" TEXT NOT NULL,
            "Col8" TEXT NOT NULL,
            "Col9" TEXT NOT NULL,
            "Col10" TEXT NOT NULL,
            "Col11" TEXT NOT NULL,
            "Col12" TEXT NOT NULL,
            "Col13" TEXT NOT NULL,
            "Col14" TEXT NOT NULL,
            "Col15" TEXT NOT NULL,
            "Col16" TEXT NOT NULL,
            "Col17" TEXT NOT NULL,
            "Col18" TEXT NOT NULL,
            "Col19" TEXT NOT NULL,
            "Col20" TEXT NOT NULL,
            "Col21" TEXT NOT NULL,
            "Col22" TEXT NOT NULL,
            "Col23" TEXT NOT NULL,
            CONSTRAINT "FK_GenericRows_Notes_NoteId" FOREIGN KEY ("NoteId")
                REFERENCES "Notes" ("Id") ON DELETE CASCADE
        );
        """;
    const string createIndex = """
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_GenericRows_NoteId_Position"
            ON "GenericRows" ("NoteId", "Position");
        """;

    db.Database.ExecuteSqlRaw(createTable);
    db.Database.ExecuteSqlRaw(createIndex);
}

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
