using Microsoft.EntityFrameworkCore;
using TableNotes.Server.Data;
using TableNotes.Shared;

namespace TableNotes.Server.Api;

public static class NotesEndpoints
{
    public static void MapNotesApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }));

        app.MapGet("/api/pages/{page}/notes", async (string page, AppDbContext db) =>
        {
            if (!PageNames.IsValid(page))
                return Results.BadRequest(new { error = $"Unknown page '{page}'." });

            var notes = await db.Notes.AsNoTracking()
                .Where(n => n.PageName == page)
                .OrderByDescending(n => n.ModifiedAtUtc)
                .ToListAsync();

            return Results.Ok(notes.Select(NoteMapping.ToDto).ToList());
        });

        app.MapGet("/api/notes/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var note = await db.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id);
            return note is null ? Results.NotFound() : Results.Ok(NoteMapping.ToDto(note));
        });

        app.MapPost("/api/pages/{page}/notes", async (string page, CreateNoteRequest req, AppDbContext db) =>
        {
            if (!PageNames.IsValid(page))
                return Results.BadRequest(new { error = $"Unknown page '{page}'." });

            var title = (req.Title ?? string.Empty).Trim();
            if (title.Length == 0)
                return Results.BadRequest(new { error = "Title is required." });

            var now = DateTime.UtcNow;
            var note = new Note
            {
                PageName = page,
                Title = title,
                Category = string.IsNullOrWhiteSpace(req.Category) ? "General" : req.Category.Trim(),
                CreatedAtUtc = now,
                ModifiedAtUtc = now,
                Version = 1
            };

            db.Notes.Add(note);
            await db.SaveChangesAsync();
            return Results.Created($"/api/notes/{note.Id}", NoteMapping.ToDto(note));
        });

        app.MapPut("/api/notes/{id:guid}", async (Guid id, UpdateNoteRequest req, AppDbContext db) =>
        {
            var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id);
            if (note is null)
                return Results.NotFound();

            var title = (req.Title ?? string.Empty).Trim();
            if (title.Length == 0)
                return Results.BadRequest(new { error = "Title is required." });

            if (note.Version != req.BaseVersion)
                return Results.Conflict(new { note.Version });

            db.Entry(note).Property(n => n.Version).OriginalValue = req.BaseVersion;
            note.Title = title;
            note.Category = string.IsNullOrWhiteSpace(req.Category) ? note.Category : req.Category.Trim();
            note.ModifiedAtUtc = DateTime.UtcNow;
            note.Version = req.BaseVersion + 1;

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Conflict(new { note.Version });
            }

            return Results.Ok(NoteMapping.ToDto(note));
        });

        app.MapDelete("/api/notes/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id);
            if (note is null)
                return Results.NotFound();

            db.Notes.Remove(note);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
