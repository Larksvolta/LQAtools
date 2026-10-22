using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TableNotes.Shared;

namespace TableNotes.Server.Api;

public sealed record PresenceUpdate(string User, string NoteId, int RowPosition, string ColumnProp, bool Active);

public sealed class PresenceRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, CellSelection> _selections = new();

    private static string Key(string user, string noteId, int row, string prop) => $"{user}|{noteId}|{row}|{prop}";

    public List<CellSelection> Set(CellSelection selection)
    {
        lock (_gate)
        {
            var previous = _selections.Values
                .Where(s => s.User == selection.User && s.NoteId == selection.NoteId)
                .Where(s => s.RowPosition != selection.RowPosition || s.ColumnProp != selection.ColumnProp)
                .Select(Clone)
                .ToList();

            foreach (var p in previous)
                _selections.Remove(Key(p.User, p.NoteId, p.RowPosition, p.ColumnProp));

            _selections[Key(selection.User, selection.NoteId, selection.RowPosition, selection.ColumnProp)] = selection;
            return previous;
        }
    }

    public void Clear(string user, string noteId, int row, string prop)
    {
        lock (_gate)
        {
            _selections.Remove(Key(user, noteId, row, prop));
        }
    }

    public void ClearUser(string user)
    {
        lock (_gate)
        {
            var stale = _selections.Where(kv => kv.Value.User == user).Select(kv => kv.Key).ToList();
            foreach (var key in stale)
                _selections.Remove(key);
        }
    }

    public PresenceSnapshot Snapshot(string? noteId)
    {
        lock (_gate)
        {
            var items = string.IsNullOrEmpty(noteId)
                ? _selections.Values
                : _selections.Values.Where(s => s.NoteId == noteId);
            return new PresenceSnapshot { Selections = items.Select(Clone).ToList() };
        }
    }

    private static CellSelection Clone(CellSelection s) => new()
    {
        User = s.User,
        NoteId = s.NoteId,
        RowPosition = s.RowPosition,
        ColumnProp = s.ColumnProp,
    };
}

public sealed class PresenceHub : Hub
{
    private readonly PresenceRegistry _registry;
    public PresenceHub(PresenceRegistry registry) => _registry = registry;

    private string ConnectionUser =>
        Context.User?.Identity?.Name ?? Context.GetHttpContext()?.User?.Identity?.Name ?? "";

    public override async Task OnConnectedAsync()
    {
        var noteId = Context.GetHttpContext()?.Request.Query["noteId"].ToString();
        await Clients.Caller.SendAsync("Snapshot", _registry.Snapshot(noteId));
        await base.OnConnectedAsync();
    }

    public async Task Update(string noteId, int rowPosition, string columnProp, bool active)
    {
        var user = ConnectionUser;
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(noteId))
            return;

        if (active)
        {
            var removed = _registry.Set(new CellSelection { User = user, NoteId = noteId, RowPosition = rowPosition, ColumnProp = columnProp });
            foreach (var old in removed)
                await Clients.Others.SendAsync("Presence", new PresenceUpdate(user, old.NoteId, old.RowPosition, old.ColumnProp, false));
        }
        else
        {
            _registry.Clear(user, noteId, rowPosition, columnProp);
        }

        await Clients.Others.SendAsync("Presence", new PresenceUpdate(user, noteId, rowPosition, columnProp, active));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var user = ConnectionUser;
        if (!string.IsNullOrEmpty(user))
        {
            _registry.ClearUser(user);
            await Clients.Others.SendAsync("UserLeft", user);
        }
        await base.OnDisconnectedAsync(exception);
    }
}

public static class PresenceEndpoints
{
    public static void MapPresenceApi(this WebApplication app)
    {
        app.MapHub<PresenceHub>("/hubs/presence");

        app.MapGet("/api/presence", (string? noteId, PresenceRegistry registry) => registry.Snapshot(noteId));
    }
}
