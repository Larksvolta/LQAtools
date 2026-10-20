using Microsoft.EntityFrameworkCore;
using TableNotes.Server.Data;
using TableNotes.Shared;

namespace TableNotes.Server.Api;

public static class CellMarkingEndpoints
{
    public static void MapCellMarkingApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/pages/{page}/cellmarkings", async (string page, AppDbContext db) =>
        {
            if (!PageNames.IsValid(page))
                return Results.BadRequest(new { error = $"Unknown page '{page}'." });

            var noteIds = db.Notes.Where(n => n.PageName == page).Select(n => n.Id);
            var items = await db.CellMarkings.AsNoTracking()
                .Where(c => noteIds.Contains(c.NoteId))
                .ToListAsync();

            return Results.Ok(items.Select(ToDto).ToList());
        });

        app.MapPut("/api/pages/{page}/cellmarkings", async (string page, List<CellMarkingDto> items, AppDbContext db) =>
        {
            if (!PageNames.IsValid(page))
                return Results.BadRequest(new { error = $"Unknown page '{page}'." });

            var validNoteIds = await db.Notes
                .Where(n => n.PageName == page)
                .Select(n => n.Id)
                .ToListAsync();

            await using var tx = await db.Database.BeginTransactionAsync();

            var existing = await db.CellMarkings
                .Where(c => validNoteIds.Contains(c.NoteId))
                .ToListAsync();
            db.CellMarkings.RemoveRange(existing);

            var now = DateTime.UtcNow;
            var seen = new HashSet<(Guid, int, string)>();
            foreach (var item in items ?? new List<CellMarkingDto>())
            {
                if (!validNoteIds.Contains(item.NoteId))
                    continue;
                if (string.IsNullOrWhiteSpace(item.ColumnProp) || string.IsNullOrWhiteSpace(item.Color))
                    continue;
                if (!seen.Add((item.NoteId, item.RowPosition, item.ColumnProp)))
                    continue;

                db.CellMarkings.Add(new CellMarking
                {
                    NoteId = item.NoteId,
                    RowPosition = item.RowPosition,
                    ColumnProp = item.ColumnProp,
                    Color = item.Color,
                    UpdatedAtUtc = now
                });
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Results.NoContent();
        });
    }

    private static CellMarkingDto ToDto(CellMarking c) => new()
    {
        NoteId = c.NoteId,
        RowPosition = c.RowPosition,
        ColumnProp = c.ColumnProp,
        Color = c.Color
    };
}
