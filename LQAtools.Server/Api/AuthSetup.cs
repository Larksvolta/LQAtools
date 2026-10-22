using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using LQAtools.Server.Data;

namespace LQAtools.Server.Api;

public static class AuthSetup
{
    public const string UserOverrideHeader = "X-LQAtools-User";

    public static bool IsEnabled(IConfiguration config) =>
        config.GetValue("Auth:Enabled", true);

    public static bool IsUserOverrideEnabled(IConfiguration config) =>
        config.GetValue("Auth:AllowUserOverride", false);

    public static void AddLQAtoolsAuth(this IServiceCollection services, IConfiguration config)
    {
        services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();

        var allowedGroupSid = ResolveAllowedGroupSid(config);

        services.AddAuthorization(options =>
        {
            if (IsEnabled(config))
            {
                var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser();

                if (allowedGroupSid is not null)
                {
                    policy = policy.RequireAssertion(context => IsMemberOf(context.User, allowedGroupSid));
                }

                options.FallbackPolicy = policy.Build();
            }
        });

        services.AddScoped<UserDirectory>();
    }

    public static void UseLQAtoolsAuth(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LQAtools.Auth");

        if (!IsEnabled(app.Configuration))
            logger.LogWarning("Authentication is DISABLED (Auth:Enabled=false): every caller can read and modify all data.");
        if (IsUserOverrideEnabled(app.Configuration))
            logger.LogWarning("Identity override is ENABLED (Auth:AllowUserOverride=true): any loopback caller can act as any user.");

        app.UseAuthentication();

        if (IsUserOverrideEnabled(app.Configuration))
            app.UseMiddleware<UserOverrideMiddleware>();

        app.UseAuthorization();

        if (IsEnabled(app.Configuration))
            app.UseMiddleware<UserActivityMiddleware>();
    }

    private static string? ResolveAllowedGroupSid(IConfiguration config)
    {
        var group = config["Auth:AllowedGroup"];

        // Claims from Windows authentication carry group SIDs; compare case-insensitively.
        if (string.IsNullOrWhiteSpace(group))
            return null;

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Auth:AllowedGroup requires Windows.");

        try
        {
            var account = new NTAccount(group.Trim());
            return ((SecurityIdentifier)account.Translate(typeof(SecurityIdentifier))).Value;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Auth:AllowedGroup '{group}' could not be resolved to a Windows group: {ex.Message}", ex);
        }
    }

    private static bool IsMemberOf(ClaimsPrincipal user, string groupSid) =>
        user.HasClaim(c =>
            (c.Type == ClaimTypes.GroupSid || c.Type == ClaimTypes.PrimaryGroupSid)
            && string.Equals(c.Value, groupSid, StringComparison.OrdinalIgnoreCase));
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
