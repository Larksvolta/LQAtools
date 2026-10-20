using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using TableNotes.Server.Data;

namespace TableNotes.Server.Api;

public static class AuthSetup
{
    public const string UserOverrideHeader = "X-TableNotes-User";

    public static bool IsEnabled(IConfiguration config) =>
        config.GetValue("Auth:Enabled", true);

    public static bool IsUserOverrideEnabled(IConfiguration config) =>
        config.GetValue("Auth:AllowUserOverride", false);

    public static void AddTableNotesAuth(this IServiceCollection services, IConfiguration config)
    {
        services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();

        services.AddAuthorization(options =>
        {
            if (IsEnabled(config))
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            }
        });

        services.AddScoped<UserDirectory>();
    }

    public static void UseTableNotesAuth(this WebApplication app)
    {
        app.UseAuthentication();

        if (IsUserOverrideEnabled(app.Configuration))
            app.UseMiddleware<UserOverrideMiddleware>();

        app.UseAuthorization();

        if (IsEnabled(app.Configuration))
            app.UseMiddleware<UserActivityMiddleware>();
    }
}

public sealed class UserOverrideMiddleware
{
    private readonly RequestDelegate _next;
    public UserOverrideMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;
        var isLoopback = remote is not null && System.Net.IPAddress.IsLoopback(remote);

        if (isLoopback
            && context.Request.Headers.TryGetValue(AuthSetup.UserOverrideHeader, out var values))
        {
            var name = values.ToString().Trim();
            if (name.Length > 0)
            {
                var identity = new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Name, name) },
                    authenticationType: "DevOverride");
                context.User = new ClaimsPrincipal(identity);
            }
        }

        await _next(context);
    }
}

public sealed class UserDirectory
{
    private readonly AppDbContext _db;
    public UserDirectory(AppDbContext db) => _db = db;

    public async Task TouchAsync(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        var now = DateTime.UtcNow;
        var user = await _db.Users.FindAsync(name);
        if (user is null)
        {
            _db.Users.Add(new AppUser { Name = name, FirstSeenAtUtc = now, LastSeenAtUtc = now });
        }
        else if ((now - user.LastSeenAtUtc).TotalMinutes >= 1)
        {
            user.LastSeenAtUtc = now;
        }
        else
        {
            return;
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            // Raced with another request inserting the same user; safe to ignore.
        }
    }
}

public sealed class UserActivityMiddleware
{
    private readonly RequestDelegate _next;
    public UserActivityMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, UserDirectory directory)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
            await directory.TouchAsync(context.User.Identity.Name);

        await _next(context);
    }
}
