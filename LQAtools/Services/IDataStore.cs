using LQAtools.Models;

namespace LQAtools.Services;

public sealed record ScreenshotInfo(string Language, string FileName, string LocalPath);

public sealed record UserInfo(string Name, DateTimeOffset FirstSeenAt, DateTimeOffset LastSeenAt);

public interface IDataStore
{
    string PageName { get; }

    Task<List<TableNote>> LoadNotesAsync();

    Task<TableNote> CreateNoteAsync(string title, string category = "General");

    Task<TableNote> SaveNoteAsync(TableNote note, string title, string category = "General");

    Task DeleteNoteAsync(TableNote note);

    Task<List<TableRow>> LoadRowsAsync(TableNote note);

    Task<TableNote> SaveRowsAsync(TableNote note, IReadOnlyList<TableRow> rows);

    Task<Dictionary<string, string>> LoadCellMarkingsAsync();

    Task SaveCellMarkingsAsync(IReadOnlyDictionary<string, string> markings);

    Task<List<ScreenshotInfo>> GetScreenshotsAsync(TableNote note, string bugId, string language);

    Task<List<string>> GetLanguagesWithScreenshotsAsync(TableNote note, string bugId);

    Task AddScreenshotAsync(TableNote note, string bugId, string language, string sourceFilePath);

    Task<string?> GetCurrentUserAsync();

    Task<List<UserInfo>> GetKnownUsersAsync();
}
