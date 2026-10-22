using System.Net;
using Microsoft.Extensions.Primitives;

namespace LQAtools.Server.Api;

public static class Hardening
{
    public static void UseLQAtoolsHardening(this WebApplication app)
    {
        var config = app.Configuration;
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LQAtools.Security");

        var requireHttpsForRemote = config.GetValue("Security:RequireHttpsForRemote", true);
        var allowInsecureRemote = config.GetValue("Security:AllowInsecureRemote", false);

        var allowedHosts = new HashSet<string>(
            config.GetSection("Security:AllowedHosts").Get<string[]>() ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(Environment.MachineName))
            allowedHosts.Add(Environment.MachineName);

        if (!requireHttpsForRemote)
            logger.LogWarning("Security:RequireHttpsForRemote is disabled: Windows credentials can be sent over plain HTTP from non-local clients.");
        if (allowInsecureRemote)
            logger.LogWarning("Security:AllowInsecureRemote is enabled: plain HTTP is accepted from non-local clients.");

        app.Use(async (context, next) =>
        {
            var request = context.Request;

            if (!IsAllowedHost(request.Host.Host, allowedHosts))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Unknown Host header.");
                return;
            }

            if (requireHttpsForRemote && !allowInsecureRemote
                && !IsSecureRequest(context) && !IsLocal(context))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("HTTPS is required for non-local connections.");
                return;
            }

            if (!IsSafeMethod(request.Method) && IsCrossSiteRequest(context))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Cross-site request rejected.");
                return;
            }

            context.Response.Headers["X-Content-Type-Options"] = "nosniff";

            await next();
        });
    }

    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

    private static bool IsLocal(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;
        return remote is null || IPAddress.IsLoopback(remote);
    }

    private static bool IsSecureRequest(HttpContext context)
    {
        if (context.Request.IsHttps)
            return true;

        return IsLocal(context)
               && context.Request.Headers.TryGetValue("X-Forwarded-Proto", out var proto)
               && proto.ToString().Trim().StartsWith("https", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedHost(string? host, HashSet<string> allowedHosts)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        host = host.Trim();
        if (host.StartsWith('[') && host.EndsWith(']'))
            host = host[1..^1];

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;
        if (IPAddress.TryParse(host, out _))
            return true;
        return allowedHosts.Contains(host);
    }

    private static bool IsCrossSiteRequest(HttpContext context)
    {
        var request = context.Request;

        if (request.Headers.TryGetValue("Sec-Fetch-Site", out var site)
            && string.Equals(site.ToString().Trim(), "cross-site", StringComparison.OrdinalIgnoreCase))
            return true;

        if (request.Headers.TryGetValue("Origin", out var origin))
            return !IsSameOrigin(origin, request);

        if (request.Headers.TryGetValue("Referer", out var referer))
            return !IsSameOrigin(referer, request);

        return false;
    }

    private static bool IsSameOrigin(StringValues value, HttpRequest request)
    {
        var text = value.ToString().Trim();
        if (text.Length == 0 || string.Equals(text, "null", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            return false;

        return string.Equals(uri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase)
               && uri.Port == (request.Host.Port ?? (request.IsHttps ? 443 : 80));
    }
}
