using Microsoft.EntityFrameworkCore;
using LQAtools.Server.Data;
using LQAtools.Shared;

namespace LQAtools.Server.Api;

public static class CellMarkingEndpoints
{
    private const int MaxItems = 20000;
    private const int MaxColumnPropLength = 20;

    private static readonly HashSet<string> AllowedColors =
        new(StringComparer.OrdinalIgnoreCase) { "red", "green", "yellow" };

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
            if ((items?.Count ?? 0) > MaxItems)
                return Results.BadRequest(new { error = $"At most {MaxItems} cell markings can be saved at once." });

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
                if (item.ColumnProp.Length > MaxColumnPropLength || !item.ColumnProp.All(char.IsLetterOrDigit))
                    continue;
                if (!AllowedColors.Contains(item.Color.Trim()))
                    continue;
                if (!seen.Add((item.NoteId, item.RowPosition, item.ColumnProp)))
                    continue;

                db.CellMarkings.Add(new CellMarking
                {
                    NoteId = item.NoteId,
                    RowPosition = item.RowPosition,
                    ColumnProp = item.ColumnProp,
                    Color = item.Color.Trim().ToLowerInvariant(),
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
