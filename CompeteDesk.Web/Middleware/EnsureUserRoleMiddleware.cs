using Microsoft.AspNetCore.Identity;

namespace CompeteDesk.Middleware;

public sealed class EnsureUserRoleMiddleware
{
    private readonly RequestDelegate _next;

    public EnsureUserRoleMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            using var scope = context.RequestServices.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

            var user = await userManager.GetUserAsync(context.User);
            if (user is not null)
            {
                await IdentitySeeder.EnsureUserHasDefaultRoleAsync(scope.ServiceProvider, user);
            }
        }

        await _next(context);
    }
}
