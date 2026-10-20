using Microsoft.EntityFrameworkCore;
using TableNotes.Server.Data;
using TableNotes.Server.Storage;
using TableNotes.Shared;

namespace TableNotes.Server.Api;

public static class ScreenshotEndpoints
{
    public static void MapScreenshotApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/notes/{noteId:guid}/screenshots",
            async (Guid noteId, string? bugId, string? language, AppDbContext db) =>
        {
            if (!await db.Notes.AnyAsync(n => n.Id == noteId))
                return Results.NotFound();

            var query = db.Screenshots.AsNoTracking()
                .Where(s => s.NoteId == noteId && s.DeletedAtUtc == null);

            if (!string.IsNullOrWhiteSpace(bugId))
                query = query.Where(s => s.BugId == bugId);
            if (!string.IsNullOrWhiteSpace(language))
                query = query.Where(s => s.Language == language);

            var items = await query.OrderBy(s => s.UploadedAtUtc).ToListAsync();
            return Results.Ok(items.Select(ToDto).ToList());
        });

        app.MapPost("/api/notes/{noteId:guid}/screenshots",
            async (Guid noteId, string? bugId, string? language, HttpRequest request,
                   AppDbContext db, IBlobStore blobs, CancellationToken ct) =>
        {
            if (!await db.Notes.AnyAsync(n => n.Id == noteId, ct))
                return Results.NotFound();
            if (string.IsNullOrWhiteSpace(bugId))
                return Results.BadRequest(new { error = "bugId is required." });
            if (string.IsNullOrWhiteSpace(language))
                return Results.BadRequest(new { error = "language is required." });
            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "Expected multipart/form-data." });

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "A non-empty file is required." });

            await using var stream = file.OpenReadStream();
            var stored = await blobs.SaveAsync(stream, file.FileName, file.ContentType, ct);

            var shot = new Screenshot
            {
                NoteId = noteId,
                BugId = bugId.Trim(),
                Language = language.Trim(),
                FileName = Path.GetFileName(file.FileName),
                ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "application/octet-stream"
                    : file.ContentType,
                SizeBytes = stored.SizeBytes,
                ContentHash = stored.ContentHash,
                BlobKey = stored.Key,
                UploadedAtUtc = DateTime.UtcNow
            };

            db.Screenshots.Add(shot);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/screenshots/{shot.Id}", ToDto(shot));
        });

        app.MapGet("/api/screenshots/{id:guid}/content",
            async (Guid id, AppDbContext db, IBlobStore blobs, CancellationToken ct) =>
        {
            var shot = await db.Screenshots.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAtUtc == null, ct);
            if (shot is null)
                return Results.NotFound();

            var stream = await blobs.OpenReadAsync(shot.BlobKey, ct);
            if (stream is null)
                return Results.NotFound();

            return Results.File(stream, shot.ContentType, shot.FileName);
        });

        app.MapDelete("/api/screenshots/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var shot = await db.Screenshots.FirstOrDefaultAsync(s => s.Id == id);
            if (shot is null)
                return Results.NotFound();

            shot.DeletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static ScreenshotDto ToDto(Screenshot s) => new()
    {
        Id = s.Id,
        NoteId = s.NoteId,
        BugId = s.BugId,
        Language = s.Language,
        FileName = s.FileName,
        ContentType = s.ContentType,
        SizeBytes = s.SizeBytes,
        ContentHash = s.ContentHash,
        UploadedAt = new DateTimeOffset(DateTime.SpecifyKind(s.UploadedAtUtc, DateTimeKind.Utc))
    };
}
