using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using TableNotes.Models;
using TableNotes.Shared;

namespace TableNotes.Services;

public sealed class RemoteDataStore : IDataStore
{
    private readonly HttpClient _http;
    private readonly string _cacheDir;
    private readonly Dictionary<Guid, TableNote> _notesById = new();

    public string PageName { get; }

    public RemoteDataStore(string pageName, string baseUrl)
    {
        PageName = pageName;
        var handler = new HttpClientHandler
        {
            UseDefaultCredentials = true,
            PreAuthenticate = true
        };
        _http = new HttpClient(handler) { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };

        var overrideUser = Environment.GetEnvironmentVariable("TABLENOTES_USER");
        if (!string.IsNullOrWhiteSpace(overrideUser))
            _http.DefaultRequestHeaders.Add("X-TableNotes-User", overrideUser.Trim());

        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TableNotes", "ScreenshotsCache");
    }

    private static Guid NoteId(TableNote note) =>
        Guid.TryParse(note.FileName, out var id) ? id : note.Id;

    public async Task<List<TableNote>> LoadNotesAsync()
    {
        var dtos = await _http.GetFromJsonAsync<List<NoteDto>>($"api/pages/{PageName}/notes")
                   ?? new List<NoteDto>();

        var notes = new List<TableNote>();
        foreach (var dto in dtos)
        {
            var note = ToNote(dto);
            _notesById[note.Id] = note;
            notes.Add(note);
        }
        return notes;
    }

    public async Task<TableNote> CreateNoteAsync(string title, string category = "General")
    {
        var response = await _http.PostAsJsonAsync(
            $"api/pages/{PageName}/notes", new CreateNoteRequest(title, category));
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<NoteDto>()
                  ?? throw new InvalidOperationException("Empty response creating note.");
        var note = ToNote(dto);
        _notesById[note.Id] = note;
        return note;
    }

    public async Task<TableNote> SaveNoteAsync(TableNote note, string title, string category = "General")
    {
        var response = await _http.PutAsJsonAsync(
            $"api/notes/{NoteId(note)}", new UpdateNoteRequest(title, category, note.Version));
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            throw new ConcurrencyConflictException("The note was changed by someone else.");

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<NoteDto>() ?? new NoteDto();
        note.Title = dto.Title;
        note.Category = dto.Category;
        note.ModifiedAt = dto.ModifiedAt.LocalDateTime;
        note.Version = dto.Version;
        return note;
    }

    public async Task DeleteNoteAsync(TableNote note)
    {
        var response = await _http.DeleteAsync($"api/notes/{NoteId(note)}");
        if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
            response.EnsureSuccessStatusCode();
        _notesById.Remove(NoteId(note));
    }

    public async Task<List<TableRow>> LoadRowsAsync(TableNote note)
    {
        var response = await _http.GetFromJsonAsync<RowsResponse>($"api/notes/{NoteId(note)}/rows")
                       ?? new RowsResponse();
        note.Version = response.Version;
        return response.Rows.Select(ToRow).ToList();
    }

    public async Task<TableNote> SaveRowsAsync(TableNote note, IReadOnlyList<TableRow> rows)
    {
        var payload = new SaveRowsRequest(note.Version, rows.Select(ToDto).ToList());
        var response = await _http.PutAsJsonAsync($"api/notes/{NoteId(note)}/rows", payload);

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            throw new ConcurrencyConflictException("This page was changed by someone else.");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<RowsResponse>() ?? new RowsResponse();
        note.Version = result.Version;
        return note;
    }

    public async Task<Dictionary<string, string>> LoadCellMarkingsAsync()
    {
        var dtos = await _http.GetFromJsonAsync<List<CellMarkingDto>>(
                       $"api/pages/{PageName}/cellmarkings")
                   ?? new List<CellMarkingDto>();

        var result = new Dictionary<string, string>();
        foreach (var dto in dtos)
            result[$"{PageName}|{dto.NoteId}|{dto.RowPosition}|{dto.ColumnProp}"] = dto.Color;
        return result;
    }

    public async Task SaveCellMarkingsAsync(IReadOnlyDictionary<string, string> markings)
    {
        var list = new List<CellMarkingDto>(markings.Count);
        foreach (var kvp in markings)
        {
            var parts = kvp.Key.Split('|');
            if (parts.Length != 4 || !Guid.TryParse(parts[1], out var noteId)
                || !int.TryParse(parts[2], out var position))
                continue;

            list.Add(new CellMarkingDto
            {
                NoteId = noteId,
                RowPosition = position,
                ColumnProp = parts[3],
                Color = kvp.Value
            });
        }

        var response = await _http.PutAsJsonAsync($"api/pages/{PageName}/cellmarkings", list);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<ScreenshotInfo>> GetScreenshotsAsync(TableNote note, string bugId, string language)
    {
        var dtos = await _http.GetFromJsonAsync<List<ScreenshotDto>>(
                       $"api/notes/{NoteId(note)}/screenshots?bugId={Uri.EscapeDataString(bugId)}" +
                       $"&language={Uri.EscapeDataString(language)}")
                   ?? new List<ScreenshotDto>();

        var result = new List<ScreenshotInfo>(dtos.Count);
        foreach (var dto in dtos)
        {
            var path = await DownloadAsync(dto);
            if (path is not null)
                result.Add(new ScreenshotInfo(dto.Language, dto.FileName, path));
        }
        return result;
    }

    public async Task<List<string>> GetLanguagesWithScreenshotsAsync(TableNote note, string bugId)
    {
        var dtos = await _http.GetFromJsonAsync<List<ScreenshotDto>>(
                       $"api/notes/{NoteId(note)}/screenshots?bugId={Uri.EscapeDataString(bugId)}")
                   ?? new List<ScreenshotDto>();

        return dtos.Select(d => d.Language).Distinct().ToList();
    }

    public async Task AddScreenshotAsync(TableNote note, string bugId, string language, string sourceFilePath)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = File.OpenRead(sourceFilePath);
        var fileContent = new StreamContent(stream);
        content.Add(fileContent, "file", Path.GetFileName(sourceFilePath));

        var url = $"api/notes/{NoteId(note)}/screenshots" +
                  $"?bugId={Uri.EscapeDataString(bugId)}&language={Uri.EscapeDataString(language)}";
        var response = await _http.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
    }

    private async Task<string?> DownloadAsync(ScreenshotDto dto)
    {
        Directory.CreateDirectory(_cacheDir);
        var ext = Path.GetExtension(dto.FileName);
        if (string.IsNullOrEmpty(ext))
            ext = ".png";
        var path = Path.Combine(_cacheDir, dto.ContentHash + ext);
        if (File.Exists(path))
            return path;

        var bytes = await _http.GetByteArrayAsync($"api/screenshots/{dto.Id}/content");
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.Equals(hash, dto.ContentHash, StringComparison.OrdinalIgnoreCase))
            return null;

        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }

    public async Task<string?> GetCurrentUserAsync()
    {
        var me = await _http.GetFromJsonAsync<MeResponse>("api/me");
        return me?.Authenticated == true ? me.Name : null;
    }

    public async Task<List<UserInfo>> GetKnownUsersAsync()
    {
        var dtos = await _http.GetFromJsonAsync<List<UserDto>>("api/users") ?? new List<UserDto>();
        return dtos.Select(d => new UserInfo(d.Name, d.FirstSeenAt, d.LastSeenAt)).ToList();
    }

    private static TableNote ToNote(NoteDto dto) => new()
    {
        Id = dto.Id,
        FileName = dto.Id.ToString(),
        Title = dto.Title,
        Category = dto.Category,
        CreatedAt = dto.CreatedAt.LocalDateTime,
        ModifiedAt = dto.ModifiedAt.LocalDateTime,
        Version = dto.Version,
        IsPlaceholder = dto.IsPlaceholder
    };

    private static TableRow ToRow(RowDto dto) => new()
    {
        Id = dto.Id,
        Version = dto.Version,
        Col1 = dto.Col1, Col2 = dto.Col2, Col3 = dto.Col3, Col4 = dto.Col4,
        Col5 = dto.Col5, Col6 = dto.Col6, Col7 = dto.Col7, Col8 = dto.Col8,
        Col9 = dto.Col9, Col10 = dto.Col10, Col11 = dto.Col11, Col12 = dto.Col12,
        Col13 = dto.Col13, Col14 = dto.Col14, Col15 = dto.Col15, Col16 = dto.Col16,
        Col17 = dto.Col17, Col18 = dto.Col18, Col19 = dto.Col19, Col20 = dto.Col20,
        Col21 = dto.Col21, Col22 = dto.Col22, Col23 = dto.Col23
    };

    private static RowDto ToDto(TableRow row) => new()
    {
        Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id,
        Version = row.Version,
        Col1 = row.Col1 ?? "", Col2 = row.Col2 ?? "", Col3 = row.Col3 ?? "", Col4 = row.Col4 ?? "",
        Col5 = row.Col5 ?? "", Col6 = row.Col6 ?? "", Col7 = row.Col7 ?? "", Col8 = row.Col8 ?? "",
        Col9 = row.Col9 ?? "", Col10 = row.Col10 ?? "", Col11 = row.Col11 ?? "", Col12 = row.Col12 ?? "",
        Col13 = row.Col13 ?? "", Col14 = row.Col14 ?? "", Col15 = row.Col15 ?? "", Col16 = row.Col16 ?? "",
        Col17 = row.Col17 ?? "", Col18 = row.Col18 ?? "", Col19 = row.Col19 ?? "", Col20 = row.Col20 ?? "",
        Col21 = row.Col21 ?? "", Col22 = row.Col22 ?? "", Col23 = row.Col23 ?? ""
    };
}

public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message) : base(message) { }
}
