using System.Collections.Concurrent;
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
                ? Enumerable.Empty<CellSelection>()
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
    private const int MaxNoteIdLength = 100;
    private const int MaxColumnPropLength = 20;

    private static readonly ConcurrentDictionary<string, string> ConnectionNotes = new();

    private readonly PresenceRegistry _registry;
    public PresenceHub(PresenceRegistry registry) => _registry = registry;

    private string ConnectionUser =>
        Context.User?.Identity?.Name ?? Context.GetHttpContext()?.User?.Identity?.Name ?? "";

    private static string GroupOf(string noteId) => "note:" + noteId;

    public override async Task OnConnectedAsync()
    {
        var noteId = NormalizeNoteId(Context.GetHttpContext()?.Request.Query["noteId"].ToString());

        if (noteId.Length > 0)
        {
            ConnectionNotes[Context.ConnectionId] = noteId;
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupOf(noteId));
        }

        await Clients.Caller.SendAsync("Snapshot", _registry.Snapshot(noteId));
        await base.OnConnectedAsync();
    }

    public async Task<PresenceSnapshot> Subscribe(string? noteId)
    {
        noteId = NormalizeNoteId(noteId);

        if (ConnectionNotes.TryRemove(Context.ConnectionId, out var previous) && previous != noteId)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupOf(previous));

        if (noteId.Length > 0)
        {
            ConnectionNotes[Context.ConnectionId] = noteId;
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupOf(noteId));
        }

        return _registry.Snapshot(noteId);
    }

    public async Task Update(string noteId, int rowPosition, string columnProp, bool active)
    {
        var user = ConnectionUser;
        noteId = NormalizeNoteId(noteId);

        if (string.IsNullOrEmpty(user) || noteId.Length == 0 || rowPosition < 0)
            return;
        if (string.IsNullOrEmpty(columnProp) || columnProp.Length > MaxColumnPropLength)
            return;

        if (!ConnectionNotes.TryGetValue(Context.ConnectionId, out var current) || current != noteId)
            await Subscribe(noteId);

        if (active)
        {
            var removed = _registry.Set(new CellSelection { User = user, NoteId = noteId, RowPosition = rowPosition, ColumnProp = columnProp });
            foreach (var old in removed)
                await Clients.OthersInGroup(GroupOf(old.NoteId)).SendAsync("Presence", new PresenceUpdate(user, old.NoteId, old.RowPosition, old.ColumnProp, false));
        }
        else
        {
            _registry.Clear(user, noteId, rowPosition, columnProp);
        }

        await Clients.OthersInGroup(GroupOf(noteId)).SendAsync("Presence", new PresenceUpdate(user, noteId, rowPosition, columnProp, active));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var user = ConnectionUser;

        if (ConnectionNotes.TryRemove(Context.ConnectionId, out var noteId))
        {
            if (!string.IsNullOrEmpty(user))
                await Clients.OthersInGroup(GroupOf(noteId)).SendAsync("UserLeft", user);
        }

        if (!string.IsNullOrEmpty(user))
            _registry.ClearUser(user);

        await base.OnDisconnectedAsync(exception);
    }

    private static string NormalizeNoteId(string? noteId)
    {
        var value = (noteId ?? string.Empty).Trim();
        if (value.Length > MaxNoteIdLength)
            return string.Empty;
        return value.All(c => char.IsLetterOrDigit(c) || c == '-') ? value : string.Empty;
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
