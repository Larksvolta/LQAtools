using System.Text.Json;
using ClosedXML.Excel;
using TableNotes.Models;

namespace TableNotes.Services;

public sealed class LocalDataStore : IDataStore
{
    private readonly ExcelService _excel;
    private readonly ScreenshotStore _screenshots;
    private readonly string _pageDir;
    private readonly string _markingsPath;
    private readonly string _screenshotsDir;

    public string PageName { get; }

    public LocalDataStore(string pageName)
    {
        PageName = pageName;
        _excel = new ExcelService(pageName);
        _pageDir = _excel.DataDirectory;
        _screenshots = new ScreenshotStore(Path.Combine(DataPaths.BasePath, "BugTracker"));
        _markingsPath = Path.Combine(DataPaths.BasePath, "cell_markings.json");
        _screenshotsDir = Path.Combine(
            Path.GetFullPath(Path.Combine(DataPaths.BasePath, "..")), "Screenshots");
    }

    private static string MasterTextsDirectory =>
        Path.Combine(DataPaths.BasePath, "MasterTexts");

    private static readonly string[] ChangelogNoteTitles =
        { "Source", "French", "Italian", "German", "Spanish" };

    public async Task<List<TableNote>> LoadNotesAsync()
    {
        var notes = await _excel.LoadIndexAsync();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deduped = new List<TableNote>();
        foreach (var n in notes)
            if (seen.Add(n.FileName))
                deduped.Add(n);

        await EnsurePlaceholdersAsync(deduped);

        return deduped.OrderByDescending(x => x.ModifiedAt).ToList();
    }

    private async Task EnsurePlaceholdersAsync(List<TableNote> notes)
    {
        if (PageName == "Changelog")
        {
            notes.Clear();
            foreach (var title in ChangelogNoteTitles)
                notes.Add(new TableNote { Title = title, FileName = $"{title}.xlsx" });
            await _excel.SaveIndexAsync(notes);
            return;
        }

        if (PageName == "TextFiles")
        {
            var masterDir = MasterTextsDirectory;
            if (!Directory.Exists(masterDir))
                return;

            var changed = false;
            foreach (var file in Directory.GetFiles(masterDir, "*.xlsx"))
            {
                var fileName = Path.GetFileName(file);
                var note = notes.FirstOrDefault(n => n.FileName == fileName);
                var targetPath = Path.Combine(_pageDir, fileName);

                if (!File.Exists(targetPath))
                    File.Copy(file, targetPath, overwrite: true);

                if (note is null)
                {
                    notes.Add(new TableNote { Title = fileName, FileName = fileName });
                    changed = true;
                }
            }

            if (changed)
                await _excel.SaveIndexAsync(notes);
            return;
        }

        if (PageName == "BugTracker")
        {
            if (notes.Count == 0)
            {
                notes.Add(new TableNote { Title = "Bug Tracker", FileName = "BugTracker.xlsx" });
                await _excel.SaveIndexAsync(notes);
            }
            return;
        }

        if (PageName != "Checklist")
            return;

        var changedChecklist = false;
        foreach (var (title, fileName) in new[] { ("Dialogue", "Dialogue.xlsx"), ("UI", "UI.xlsx") })
        {
            var path = Path.Combine(_pageDir, fileName);
            var note = notes.FirstOrDefault(n => n.FileName == fileName);

            if (!File.Exists(path))
            {
                var rows = await LoadRowsFromMasterAsync(fileName);
                if (rows.Count == 0)
                    continue;

                var obsRows = new System.Collections.ObjectModel.ObservableCollection<TableRow>(rows);
                if (note is not null)
                {
                    await _excel.SaveRowsAsync(note, obsRows);
                }
                else
                {
                    note = new TableNote { Title = title, FileName = fileName };
                    await _excel.SaveRowsAsync(note, obsRows);
                    notes.Add(note);
                    changedChecklist = true;
                }
            }
            else if (note is null)
            {
                notes.Add(new TableNote { Title = title, FileName = fileName });
                changedChecklist = true;
            }
        }

        if (changedChecklist)
            await _excel.SaveIndexAsync(notes);
    }

    private static async Task<List<TableRow>> LoadRowsFromMasterAsync(string fileName)
    {
        var masterPath = Path.Combine(MasterTextsDirectory, fileName);
        var rows = new List<TableRow>();
        if (!File.Exists(masterPath))
            return rows;

        await Task.Run(() =>
        {
            using var workbook = new XLWorkbook(masterPath);
            var ws = workbook.Worksheet(1);
            foreach (var row in ws.RowsUsed().Skip(1))
            {
                rows.Add(new TableRow
                {
                    Col1 = row.Cell(1).GetString(),
                    Col2 = row.Cell(2).GetString(),
                    Col3 = string.Empty,
                    Col4 = row.Cell(3).GetString(),
                    Col5 = row.Cell(4).GetString(),
                    Col6 = row.Cell(5).GetString(),
                    Col7 = row.Cell(6).GetString(),
                });
            }
        });

        return rows;
    }

    public async Task<TableNote> CreateNoteAsync(string title, string category = "General")
    {
        var existing = (await _excel.LoadIndexAsync()).Select(n => n.FileName).ToHashSet();
        var note = new TableNote
        {
            Title = title,
            Category = category,
            ModifiedAt = DateTime.Now,
            FileName = ExcelService.GenerateFileName(title, existing)
        };
        return note;
    }

    public async Task<TableNote> SaveNoteAsync(TableNote note, string title, string category = "General")
    {
        var oldTitle = note.Title;
        note.Title = title;
        note.Category = category;
        note.ModifiedAt = DateTime.Now;

        if (!string.IsNullOrEmpty(note.FileName) && note.Title != oldTitle)
        {
            var existing = (await _excel.LoadIndexAsync()).Select(n => n.FileName).ToHashSet();
            var newFile = ExcelService.GenerateFileName(title, existing);
            _excel.RenameFile(note, newFile);
        }

        return note;
    }

    public Task DeleteNoteAsync(TableNote note)
    {
        _excel.DeleteFile(note);
        return Task.CompletedTask;
    }

    public async Task<List<TableRow>> LoadRowsAsync(TableNote note)
    {
        var rows = await _excel.LoadRowsAsync(note);
        return rows.ToList();
    }

    public async Task<TableNote> SaveRowsAsync(TableNote note, IReadOnlyList<TableRow> rows)
    {
        await _excel.SaveRowsAsync(
            note, new System.Collections.ObjectModel.ObservableCollection<TableRow>(rows));
        return note;
    }

    public Task SaveIndexAsync(IEnumerable<TableNote> notes) =>
        _excel.SaveIndexAsync(notes.ToList());

    public Dictionary<string, string> LoadCellMarkings()
    {
        var result = new Dictionary<string, string>();
        try
        {
            if (File.Exists(_markingsPath))
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    File.ReadAllText(_markingsPath));
                if (data is not null)
                    foreach (var kvp in data)
                        result[kvp.Key] = kvp.Value;
            }
        }
        catch { }
        return result;
    }

    public Task<Dictionary<string, string>> LoadCellMarkingsAsync() =>
        Task.FromResult(LoadCellMarkings());

    public Task SaveCellMarkingsAsync(IReadOnlyDictionary<string, string> markings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_markingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(_markingsPath, JsonSerializer.Serialize(markings));
        }
        catch { }
        return Task.CompletedTask;
    }

    public Task<List<ScreenshotInfo>> GetScreenshotsAsync(TableNote note, string bugId, string language)
    {
        var result = new List<ScreenshotInfo>();
        foreach (var fileName in _screenshots.GetScreenshots(note.FileName, bugId, language))
            result.Add(new ScreenshotInfo(language, fileName,
                Path.Combine(_screenshotsDir, fileName)));
        return Task.FromResult(result);
    }

    public Task<List<string>> GetLanguagesWithScreenshotsAsync(TableNote note, string bugId) =>
        Task.FromResult(_screenshots.GetLanguagesWithScreenshots(note.FileName, bugId));

    public Task AddScreenshotAsync(TableNote note, string bugId, string language, string sourceFilePath)
    {
        try
        {
            if (!Directory.Exists(_screenshotsDir))
                Directory.CreateDirectory(_screenshotsDir);

            var baseName = Path.GetFileNameWithoutExtension(sourceFilePath);
            var sanitized = string.Concat(
                baseName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            if (string.IsNullOrEmpty(sanitized))
                sanitized = "screenshot";

            var name = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{sanitized}{Path.GetExtension(sourceFilePath)}";
            var target = Path.Combine(_screenshotsDir, name);
            File.Copy(sourceFilePath, target, overwrite: false);
            _screenshots.AddScreenshot(note.FileName, bugId, language, name);
        }
        catch { }

        return Task.CompletedTask;
    }
}
