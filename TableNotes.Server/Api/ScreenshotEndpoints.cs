using Microsoft.EntityFrameworkCore;
using TableNotes.Server.Data;
using TableNotes.Server.Storage;
using TableNotes.Shared;

namespace TableNotes.Server.Api;

public static class ScreenshotEndpoints
{
    private const long MaxScreenshotBytes = 25L * 1024 * 1024;
    private const int MaxLabelLength = 200;
    private const int MaxFileNameLength = 120;

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
            if (bugId.Trim().Length > MaxLabelLength || language.Trim().Length > MaxLabelLength)
                return Results.BadRequest(new { error = $"bugId and language must be at most {MaxLabelLength} characters." });
            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "Expected multipart/form-data." });

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "A non-empty file is required." });
            if (file.Length > MaxScreenshotBytes)
                return Results.BadRequest(new { error = "Screenshots must be at most 25 MB." });

            await using var upload = new MemoryStream((int)file.Length);
            await using (var source = file.OpenReadStream())
                await source.CopyToAsync(upload, ct);
            upload.Position = 0;

            var head = new byte[16];
            var read = await upload.ReadAtLeastAsync(head.AsMemory(), 12, throwOnEndOfStream: false, ct);
            var image = DetectImage(head.AsSpan(0, read));
            if (image is null)
                return Results.BadRequest(new { error = "Only PNG, JPEG, GIF, BMP or WebP images are accepted." });
            upload.Position = 0;

            var fileName = SafeFileName(file.FileName, image.Value.Extension);
            var stored = await blobs.SaveAsync(upload, fileName, image.Value.ContentType, ct);

            var shot = new Screenshot
            {
                NoteId = noteId,
                BugId = bugId.Trim(),
                Language = language.Trim(),
                FileName = fileName,
                ContentType = image.Value.ContentType,
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
            async (Guid id, HttpContext context, AppDbContext db, IBlobStore blobs, CancellationToken ct) =>
        {
            var shot = await db.Screenshots.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAtUtc == null, ct);
            if (shot is null)
                return Results.NotFound();

            var stream = await blobs.OpenReadAsync(shot.BlobKey, ct);
            if (stream is null)
                return Results.NotFound();

            context.Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
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

    private static readonly (byte[] Signature, int Offset, string ContentType, string Extension)[] Signatures =
    {
        (new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, "image/png", ".png"),
        (new byte[] { 0xFF, 0xD8, 0xFF }, 0, "image/jpeg", ".jpg"),
        (new byte[] { 0x47, 0x49, 0x46, 0x38 }, 0, "image/gif", ".gif"),
        (new byte[] { 0x42, 0x4D }, 0, "image/bmp", ".bmp"),
    };

    private static (string ContentType, string Extension)? DetectImage(ReadOnlySpan<byte> head)
    {
        foreach (var candidate in Signatures)
        {
            if (head.Length >= candidate.Offset + candidate.Signature.Length
                && head.Slice(candidate.Offset, candidate.Signature.Length).SequenceEqual(candidate.Signature))
            {
                return (candidate.ContentType, candidate.Extension);
            }
        }

        if (head.Length >= 12
            && head[..4].SequenceEqual("RIFF"u8)
            && head.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return ("image/webp", ".webp");
        }

        return null;
    }

    private static string SafeFileName(string? original, string extension)
    {
        var name = Path.GetFileName(original ?? string.Empty);
        var cleaned = new string(name.Where(c => !char.IsControl(c) && c != '"' && c != '\\' && c != '/').ToArray()).Trim();
        var stem = Path.GetFileNameWithoutExtension(cleaned);

        if (string.IsNullOrWhiteSpace(stem))
            stem = "screenshot";

        if (stem.Length > MaxFileNameLength)
            stem = stem[..MaxFileNameLength];

        return stem + extension;
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
