using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using LQAtools.Server.Data;
using LQAtools.Server.Storage;
using LQAtools.Shared;

namespace LQAtools.Server.Import;

public sealed class LocalDataImporter
{
    private readonly AppDbContext _db;
    private readonly IBlobStore _blobs;
    private readonly ILogger<LocalDataImporter> _log;

    public LocalDataImporter(AppDbContext db, IBlobStore blobs, ILogger<LocalDataImporter> log)
    {
        _db = db;
        _blobs = blobs;
        _log = log;
    }

    public async Task ImportAsync(string dataRoot, CancellationToken ct = default)
    {
        dataRoot = Path.GetFullPath(dataRoot);
        var screenshotsRoot = Path.GetFullPath(Path.Combine(dataRoot, "..", "Screenshots"));

        _log.LogInformation("Importing from {DataRoot}", dataRoot);

        var notesByPage = new Dictionary<string, List<ImportedNote>>(StringComparer.Ordinal);

        foreach (var page in PageNames.All)
        {
            var pageDir = Path.Combine(dataRoot, page);
            if (!Directory.Exists(pageDir))
                continue;

            var imported = await ImportPageAsync(page, pageDir, ct);
            notesByPage[page] = imported;
            await ImportScreenshotsAsync(pageDir, screenshotsRoot, imported, ct);
        }

        await ImportCellMarkingsAsync(Path.Combine(dataRoot, "cell_markings.json"), notesByPage, ct);

        _log.LogInformation("Import complete.");
    }

    private async Task<List<ImportedNote>> ImportPageAsync(string page, string pageDir, CancellationToken ct)
    {
        var result = new List<ImportedNote>();
        var indexPath = Path.Combine(pageDir, "notes.json");
        if (!File.Exists(indexPath))
        {
            _log.LogWarning("No notes.json in {Dir}", pageDir);
            return result;
        }

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(indexPath, ct));
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var fileName = GetString(element, "FileName");
            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            var title = GetString(element, "Title");
            if (string.IsNullOrWhiteSpace(title))
                title = Path.GetFileNameWithoutExtension(fileName);

            var legacyId = GetGuid(element, "Id");
            var createdAt = GetDate(element, "CreatedAt");
            var modifiedAt = GetDate(element, "ModifiedAt");
            var category = GetString(element, "Category");

            var note = legacyId == Guid.Empty
                ? null
                : await _db.Notes.FirstOrDefaultAsync(n => n.Id == legacyId, ct);
            note ??= await _db.Notes.FirstOrDefaultAsync(
                n => n.PageName == page && n.Title == title, ct);

            if (note is null)
            {
                note = new Note
                {
                    Id = legacyId == Guid.Empty ? Guid.NewGuid() : legacyId,
                    PageName = page
                };
                _db.Notes.Add(note);
            }

            note.Title = title;
            note.Category = string.IsNullOrWhiteSpace(category) ? "General" : category;
            note.CreatedAtUtc = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc);
            note.ModifiedAtUtc = DateTime.SpecifyKind(modifiedAt, DateTimeKind.Utc);
            note.IsPlaceholder = GetBool(element, "IsPlaceholder");

            await _db.SaveChangesAsync(ct);

            var xlsxPath = Path.Combine(pageDir, fileName);
            if (File.Exists(xlsxPath))
                await ImportRowsAsync(note, xlsxPath, ct);
            else
                _log.LogWarning("Missing workbook {Path}", xlsxPath);

            result.Add(new ImportedNote(note.Id, fileName));
        }

        return result;
    }

    private async Task ImportRowsAsync(Note note, string xlsxPath, CancellationToken ct)
    {
        var columnCount = RowMapping.ColumnCount(note.PageName);
        var rows = new List<RowDto>();

        using (var workbook = new XLWorkbook(xlsxPath))
        {
            var sheet = workbook.Worksheet(1);
            foreach (var row in sheet.RowsUsed().Skip(1))
            {
                var dto = new RowDto();
                for (var c = 1; c <= columnCount; c++)
                    SetColumn(dto, c, row.Cell(c).GetString());
                rows.Add(dto);
            }
        }

        await ClearRowsAsync(note);
        for (var i = 0; i < rows.Count; i++)
            AddRow(note.PageName, RowMapping.FromDto(note.PageName, rows[i], note.Id, i));

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Imported {Count} rows into {Page}/{Title}", rows.Count, note.PageName, note.Title);
    }

    private async Task ImportScreenshotsAsync(
        string pageDir, string screenshotsRoot, List<ImportedNote> notes, CancellationToken ct)
    {
        var path = Path.Combine(pageDir, "screenshots.json");
        if (!File.Exists(path))
            return;

        var byFile = notes
            .GroupBy(n => n.LegacyFileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct));
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return;

        foreach (var noteEntry in doc.RootElement.EnumerateObject())
        {
            if (!byFile.TryGetValue(noteEntry.Name, out var importedNote))
            {
                _log.LogWarning("Screenshot note not imported: {File}", noteEntry.Name);
                continue;
            }

            foreach (var bugEntry in noteEntry.Value.EnumerateObject())
            foreach (var langEntry in bugEntry.Value.EnumerateObject())
            foreach (var fileElement in langEntry.Value.EnumerateArray())
            {
                var fileName = fileElement.GetString();
                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                var exists = await _db.Screenshots.AnyAsync(s =>
                    s.NoteId == importedNote.NoteId &&
                    s.BugId == bugEntry.Name &&
                    s.Language == langEntry.Name &&
                    s.FileName == fileName, ct);
                if (exists)
                    continue;

                var sourcePath = Path.Combine(screenshotsRoot, fileName);
                if (!File.Exists(sourcePath))
                {
                    _log.LogWarning("Missing screenshot {Path}", sourcePath);
                    continue;
                }

                await using var stream = File.OpenRead(sourcePath);
                var stored = await _blobs.SaveAsync(stream, fileName, ContentTypeFor(fileName), ct);

                _db.Screenshots.Add(new Screenshot
                {
                    NoteId = importedNote.NoteId,
                    BugId = bugEntry.Name,
                    Language = langEntry.Name,
                    FileName = fileName,
                    ContentType = ContentTypeFor(fileName),
                    SizeBytes = stored.SizeBytes,
                    ContentHash = stored.ContentHash,
                    BlobKey = stored.Key,
                    UploadedAtUtc = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task ImportCellMarkingsAsync(
        string markingsPath, Dictionary<string, List<ImportedNote>> notesByPage, CancellationToken ct)
    {
        if (!File.Exists(markingsPath))
            return;

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(markingsPath, ct));
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return;

        var imported = 0;
        var skipped = 0;

        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            var parts = entry.Name.Split('|');
            if (parts.Length != 4)
            {
                skipped++;
                continue;
            }

            var page = parts[0];
            if (!notesByPage.TryGetValue(page, out var notes))
            {
                skipped++;
                continue;
            }

            var note = notes.FirstOrDefault(n =>
                string.Equals(n.LegacyFileName, parts[1], StringComparison.OrdinalIgnoreCase));
            if (note is null || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var position))
            {
                skipped++;
                continue;
            }

            var color = entry.Value.GetString();
            if (string.IsNullOrWhiteSpace(color))
                continue;

            var existing = await _db.CellMarkings.FirstOrDefaultAsync(c =>
                c.NoteId == note.NoteId && c.RowPosition == position && c.ColumnProp == parts[3], ct);
            if (existing is null)
            {
                _db.CellMarkings.Add(new CellMarking
                {
                    NoteId = note.NoteId,
                    RowPosition = position,
                    ColumnProp = parts[3],
                    Color = color,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                existing.Color = color;
            }

            imported++;
        }

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Imported {Count} cell markings ({Skipped} legacy skipped)", imported, skipped);
    }

    private async Task ClearRowsAsync(Note note)
    {
        switch (note.PageName)
        {
            case PageNames.Checklist:
                await _db.ChecklistRows.Where(r => r.NoteId == note.Id).ExecuteDeleteAsync();
                break;
            case PageNames.TextFiles:
                await _db.TextFilesRows.Where(r => r.NoteId == note.Id).ExecuteDeleteAsync();
                break;
            case PageNames.Changelog:
                await _db.ChangelogRows.Where(r => r.NoteId == note.Id).ExecuteDeleteAsync();
                break;
            case PageNames.BugTracker:
                await _db.BugTrackerRows.Where(r => r.NoteId == note.Id).ExecuteDeleteAsync();
                break;
            case PageNames.Glossary:
            case PageNames.Tasks:
            case PageNames.Compliance:
                await _db.GenericRows.Where(r => r.NoteId == note.Id).ExecuteDeleteAsync();
                break;
        }
    }

    private void AddRow(string page, INoteRow row)
    {
        switch (page)
        {
            case PageNames.Checklist:
                _db.ChecklistRows.Add((ChecklistRow)row);
                break;
            case PageNames.TextFiles:
                _db.TextFilesRows.Add((TextFilesRow)row);
                break;
            case PageNames.Changelog:
                _db.ChangelogRows.Add((ChangelogRow)row);
                break;
            case PageNames.BugTracker:
                _db.BugTrackerRows.Add((BugTrackerRow)row);
                break;
            case PageNames.Glossary:
            case PageNames.Tasks:
            case PageNames.Compliance:
                _db.GenericRows.Add((GenericRow)row);
                break;
        }
    }

    private static void SetColumn(RowDto dto, int column, string value)
    {
        switch (column)
        {
            case 1: dto.Col1 = value; break;
            case 2: dto.Col2 = value; break;
            case 3: dto.Col3 = value; break;
            case 4: dto.Col4 = value; break;
            case 5: dto.Col5 = value; break;
            case 6: dto.Col6 = value; break;
            case 7: dto.Col7 = value; break;
            case 8: dto.Col8 = value; break;
            case 9: dto.Col9 = value; break;
            case 10: dto.Col10 = value; break;
            case 11: dto.Col11 = value; break;
            case 12: dto.Col12 = value; break;
            case 13: dto.Col13 = value; break;
            case 14: dto.Col14 = value; break;
            case 15: dto.Col15 = value; break;
            case 16: dto.Col16 = value; break;
            case 17: dto.Col17 = value; break;
            case 18: dto.Col18 = value; break;
            case 19: dto.Col19 = value; break;
            case 20: dto.Col20 = value; break;
            case 21: dto.Col21 = value; break;
            case 22: dto.Col22 = value; break;
            case 23: dto.Col23 = value; break;
        }
    }

    private static string ContentTypeFor(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".tif" or ".tiff" => "image/tiff",
            _ => "application/octet-stream"
        };

    private static string GetString(JsonElement element, string name)
    {
        foreach (var p in element.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : p.Value.ToString();
        return string.Empty;
    }

    private static Guid GetGuid(JsonElement element, string name) =>
        Guid.TryParse(GetString(element, name), out var value) ? value : Guid.Empty;

    private static bool GetBool(JsonElement element, string name) =>
        bool.TryParse(GetString(element, name), out var value) && value;

    private static DateTime GetDate(JsonElement element, string name)
    {
        var raw = GetString(element, name);
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value))
            return value.ToUniversalTime();
        return DateTime.UtcNow;
    }

    private sealed record ImportedNote(Guid NoteId, string LegacyFileName);
}
