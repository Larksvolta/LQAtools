using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using LQAtools.Server.Data;
using LQAtools.Shared;

namespace LQAtools.Server.Api;

public static class AuthEndpoints
{
    public static void MapAuthApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", (HttpContext context) =>
        {
            var user = context.User;
            var authenticated = user?.Identity?.IsAuthenticated == true;
            return Results.Ok(new MeResponse
            {
                Name = authenticated ? user!.Identity!.Name : null,
                Authenticated = authenticated
            });
        });

        app.MapGet("/api/users", async (AppDbContext db) =>
        {
            var users = await db.Users.AsNoTracking()
                .OrderBy(u => u.Name)
                .ToListAsync();

            return Results.Ok(users.Select(u => new UserDto
            {
                Name = u.Name,
                FirstSeenAt = new DateTimeOffset(DateTime.SpecifyKind(u.FirstSeenAtUtc, DateTimeKind.Utc)),
                LastSeenAt = new DateTimeOffset(DateTime.SpecifyKind(u.LastSeenAtUtc, DateTimeKind.Utc))
            }).ToList());
        });
    }
}
