using Microsoft.EntityFrameworkCore;
using TableNotes.Server.Data;
using TableNotes.Shared;

namespace TableNotes.Server.Api;

public static class RowsEndpoints
{
    public static void MapRowsApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/notes/{id:guid}/rows", async (Guid id, AppDbContext db) =>
        {
            var note = await db.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id);
            if (note is null)
                return Results.NotFound();

            var rows = await LoadRowsAsync(db, note);
            return Results.Ok(new RowsResponse
            {
                Version = note.Version,
                Rows = rows.Select(r => RowMapping.ToDto(note.PageName, r)).ToList()
            });
        });

        app.MapPut("/api/notes/{id:guid}/rows", async (Guid id, SaveRowsRequest req, AppDbContext db) =>
        {
            var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id);
            if (note is null)
                return Results.NotFound();

            if (!PageNames.IsValid(note.PageName))
                return Results.BadRequest(new { error = $"Unknown page '{note.PageName}'." });

            if (note.Version != req.BaseVersion)
                return Results.Conflict(new RowsResponse { Version = note.Version });

            var rowsRequest = req.Rows ?? new List<RowDto>();
            if (rowsRequest.Select(r => r.Id).Where(x => x != Guid.Empty).Distinct().Count()
                != rowsRequest.Select(r => r.Id).Where(x => x != Guid.Empty).Count())
                return Results.BadRequest(new { error = "Duplicate row ids." });

            await using var tx = await db.Database.BeginTransactionAsync();

            await ClearRowsAsync(db, note);

            var saved = new List<INoteRow>(rowsRequest.Count);
            for (var i = 0; i < rowsRequest.Count; i++)
                saved.Add(RowMapping.FromDto(note.PageName, rowsRequest[i], note.Id, i));

            AddRows(db, note.PageName, saved);

            db.Entry(note).Property(n => n.Version).OriginalValue = req.BaseVersion;
            note.Version = req.BaseVersion + 1;
            note.ModifiedAtUtc = DateTime.UtcNow;

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync();
                var current = await db.Notes.AsNoTracking()
                    .Where(n => n.Id == id).Select(n => n.Version).FirstOrDefaultAsync();
                return Results.Conflict(new RowsResponse { Version = current });
            }

            await tx.CommitAsync();

            return Results.Ok(new RowsResponse
            {
                Version = note.Version,
                Rows = saved.Select(r => RowMapping.ToDto(note.PageName, r)).ToList()
            });
        });
    }

    private static async Task<List<INoteRow>> LoadRowsAsync(AppDbContext db, Note note) => note.PageName switch
    {
        PageNames.Checklist => (await db.ChecklistRows.AsNoTracking()
            .Where(r => r.NoteId == note.Id).OrderBy(r => r.Position).ToListAsync())
            .Cast<INoteRow>().ToList(),
        PageNames.TextFiles => (await db.TextFilesRows.AsNoTracking()
            .Where(r => r.NoteId == note.Id).OrderBy(r => r.Position).ToListAsync())
            .Cast<INoteRow>().ToList(),
        PageNames.Changelog => (await db.ChangelogRows.AsNoTracking()
            .Where(r => r.NoteId == note.Id).OrderBy(r => r.Position).ToListAsync())
            .Cast<INoteRow>().ToList(),
        PageNames.BugTracker => (await db.BugTrackerRows.AsNoTracking()
            .Where(r => r.NoteId == note.Id).OrderBy(r => r.Position).ToListAsync())
            .Cast<INoteRow>().ToList(),
        _ => new List<INoteRow>()
    };

    private static async Task ClearRowsAsync(AppDbContext db, Note note)
    {
        switch (note.PageName)
        {
            case PageNames.Checklist:
                await db.ChecklistRows.Where(r => r.NoteId == note.Id).ExecuteDeleteAsync();
                break;
            case PageNames.TextFiles:
                await db.TextFilesRows.Where(r => r.NoteId == note.Id).ExecuteDeleteAsync();
                break;
            case PageNames.Changelog:
                await db.ChangelogRows.Where(r => r.NoteId == note.Id).ExecuteDeleteAsync();
                break;
            case PageNames.BugTracker:
                await db.BugTrackerRows.Where(r => r.NoteId == note.Id).ExecuteDeleteAsync();
                break;
        }
    }

    private static void AddRows(AppDbContext db, string page, List<INoteRow> rows)
    {
        switch (page)
        {
            case PageNames.Checklist:
                db.ChecklistRows.AddRange(rows.Cast<ChecklistRow>());
                break;
            case PageNames.TextFiles:
                db.TextFilesRows.AddRange(rows.Cast<TextFilesRow>());
                break;
            case PageNames.Changelog:
                db.ChangelogRows.AddRange(rows.Cast<ChangelogRow>());
                break;
            case PageNames.BugTracker:
                db.BugTrackerRows.AddRange(rows.Cast<BugTrackerRow>());
                break;
        }
    }
}
