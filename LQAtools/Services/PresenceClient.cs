using Microsoft.AspNetCore.SignalR.Client;
using LQAtools.Shared;

namespace LQAtools.Services;

public sealed record RemotePresence(string User, string NoteId, int RowPosition, string ColumnProp, bool Active);

public sealed class PresenceClient : IAsyncDisposable
{
    private readonly Func<string?> _currentUser;
    private readonly object _gate = new();
    private HubConnection? _connection;
    private string _baseUrl = "";
    private string _noteId = "";
    private CellSelection? _last;
    private bool _started;

    public event EventHandler<PresenceSnapshot>? SnapshotReceived;
    public event EventHandler<RemotePresence>? PresenceReceived;
    public event EventHandler<string>? UserLeft;

    public PresenceClient(Func<string?> currentUser) => _currentUser = currentUser;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task StartAsync(string baseUrl, string? noteId)
    {
        try
        {
            AppDataStores.ResolveUri(baseUrl);
        }
        catch
        {
            return;
        }

        _baseUrl = baseUrl.TrimEnd('/');
        _noteId = noteId ?? "";

        if (_started)
            return;
        _started = true;

        await ConnectAsync();
    }

    public async Task SwitchNoteAsync(string? noteId)
    {
        var previous = _last;
        if (previous is not null)
            await SendAsync(previous, active: false);
        _last = null;

        _noteId = noteId ?? "";

        if (_connection is null)
        {
            await ConnectAsync();
            return;
        }

        if (_connection.State != HubConnectionState.Connected)
            return;

        try
        {
            var snapshot = await _connection.InvokeAsync<LQAtools.Shared.PresenceSnapshot>("Subscribe", _noteId);
            SnapshotReceived?.Invoke(this, snapshot);
        }
        catch
        {
            // Presence is best-effort.
        }
    }

    private async Task ConnectAsync()
    {
        var url = $"{_baseUrl}/hubs/presence?noteId={Uri.EscapeDataString(_noteId)}";

        var connection = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.UseDefaultCredentials = true;
                var overrideUser = Environment.GetEnvironmentVariable("LQATOOLS_USER");
                if (!string.IsNullOrWhiteSpace(overrideUser))
                    options.Headers["X-LQAtools-User"] = overrideUser.Trim();
            })
            .WithAutomaticReconnect()
            .Build();

        connection.On<LQAtools.Shared.PresenceSnapshot>("Snapshot", snapshot =>
            SnapshotReceived?.Invoke(this, snapshot));

        connection.On<LQAtools.Shared.PresenceUpdate>("Presence", update =>
            PresenceReceived?.Invoke(this,
                new RemotePresence(update.User, update.NoteId, update.RowPosition, update.ColumnProp, update.Active)));

        connection.On<string>("UserLeft", user =>
            UserLeft?.Invoke(this, user));

        connection.Reconnecting += _ => Task.CompletedTask;
        connection.Reconnected += async _ => await ResendAsync();
        connection.Closed += _ => Task.CompletedTask;

        lock (_gate) _connection = connection;

        try
        {
            await connection.StartAsync();
            await ResendAsync();
        }
        catch
        {
            // Server unreachable or not authenticated: presence is best-effort.
        }
    }

    public Task SelectAsync(string noteId, int rowPosition, string columnProp)
    {
        return SendAsync(new CellSelection
        {
            User = _currentUser() ?? "",
            NoteId = noteId,
            RowPosition = rowPosition,
            ColumnProp = columnProp,
        }, active: true);
    }

    private async Task SendAsync(CellSelection? selection, bool active)
    {
        var connection = _connection;
        if (connection is null || connection.State != HubConnectionState.Connected)
            return;

        var target = selection ?? _last;
        if (target is null || string.IsNullOrEmpty(target.NoteId))
            return;

        try
        {
            await connection.InvokeAsync("Update", target.NoteId, target.RowPosition, target.ColumnProp, active);
            if (active) _last = target;
            else _last = null;
        }
        catch
        {
            // Ignore transient failures; reconnect will resend.
        }
    }

    private async Task ResendAsync()
    {
        if (_last is not null)
            await SendAsync(_last, active: true);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
