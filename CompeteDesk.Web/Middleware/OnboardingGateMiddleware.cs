using System.Security.Claims;
using CompeteDesk.Data;
using CompeteDesk.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CompeteDesk.Middleware;

public sealed class OnboardingGateMiddleware
{
    private readonly RequestDelegate _next;

    public OnboardingGateMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated == true
            && (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)))
        {
            var path = context.Request.Path;
            if (!AppShellPolicy.ShouldSkipOnboardingGate(path))
            {
                var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    using var scope = context.RequestServices.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var hasProfile = await db.UserProfiles.AnyAsync(x => x.UserId == userId);
                    if (!hasProfile)
                    {
                        context.Response.Redirect("/Onboarding");
                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}
