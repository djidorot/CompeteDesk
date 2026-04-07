using CompeteDesk.Services.Security;
using Microsoft.AspNetCore.Identity;

namespace CompeteDesk.Middleware;

public sealed class FeaturePermissionMiddleware
{
    private readonly RequestDelegate _next;

    public FeaturePermissionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<IdentityUser> userManager, FeaturePermissionService permissions)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var requiredPermission = ResolvePermission(context.Request.Path, context.Request.Method);
        if (string.IsNullOrWhiteSpace(requiredPermission))
        {
            await _next(context);
            return;
        }

        var user = await userManager.GetUserAsync(context.User);
        if (user is null)
        {
            await _next(context);
            return;
        }

        if (!await permissions.HasPermissionAsync(user, requiredPermission))
        {
            context.Response.Redirect("/Identity/Account/AccessDenied");
            return;
        }

        await _next(context);
    }

    private static string? ResolvePermission(PathString path, string method)
    {
        if (!path.HasValue) return null;
        var isWrite = HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

        return path.Value switch
        {
            var p when p.StartsWith("/Dashboard", StringComparison.OrdinalIgnoreCase) => isWrite ? "dashboard.manage" : "dashboard.view",
            var p when p.StartsWith("/Workspaces", StringComparison.OrdinalIgnoreCase) => isWrite || p.Contains("/Create", StringComparison.OrdinalIgnoreCase) ? "workspaces.manage" : "workspaces.view",
            var p when p.StartsWith("/Strategies", StringComparison.OrdinalIgnoreCase) => isWrite || p.Contains("/Create", StringComparison.OrdinalIgnoreCase) || p.Contains("/Edit", StringComparison.OrdinalIgnoreCase) || p.Contains("/Delete", StringComparison.OrdinalIgnoreCase) ? "strategies.manage" : "strategies.view",
            var p when p.StartsWith("/Actions", StringComparison.OrdinalIgnoreCase) => isWrite || p.Contains("/Create", StringComparison.OrdinalIgnoreCase) || p.Contains("/Edit", StringComparison.OrdinalIgnoreCase) || p.Contains("/Delete", StringComparison.OrdinalIgnoreCase) ? "actions.manage" : "actions.view",
            var p when p.StartsWith("/Habits", StringComparison.OrdinalIgnoreCase) => isWrite || p.Contains("/Create", StringComparison.OrdinalIgnoreCase) || p.Contains("/Edit", StringComparison.OrdinalIgnoreCase) || p.Contains("/Delete", StringComparison.OrdinalIgnoreCase) ? "habits.manage" : "habits.view",
            var p when p.StartsWith("/Metrics", StringComparison.OrdinalIgnoreCase) => isWrite ? "metrics.manage" : "metrics.view",
            var p when p.StartsWith("/Recommendations", StringComparison.OrdinalIgnoreCase) => isWrite || p.Contains("CreateAction", StringComparison.OrdinalIgnoreCase) ? "recommendations.manage" : "recommendations.view",
            var p when p.StartsWith("/StudyPlanner", StringComparison.OrdinalIgnoreCase) => isWrite ? "study-planner.manage" : "study-planner.view",
            var p when p.StartsWith("/Exports", StringComparison.OrdinalIgnoreCase) => "exports.view",
            var p when p.StartsWith("/WebsiteAnalysis", StringComparison.OrdinalIgnoreCase) => isWrite ? "website-analysis.manage" : "website-analysis.view",
            var p when p.StartsWith("/BusinessAnalysis", StringComparison.OrdinalIgnoreCase) => isWrite ? "business-analysis.manage" : "business-analysis.view",
            var p when p.StartsWith("/WarRoom", StringComparison.OrdinalIgnoreCase) || p.StartsWith("/WarRoomAi", StringComparison.OrdinalIgnoreCase) => isWrite ? "war-room.manage" : "war-room.view",
            var p when p.StartsWith("/Activity", StringComparison.OrdinalIgnoreCase) => "activity.view",
            var p when p.StartsWith("/Settings", StringComparison.OrdinalIgnoreCase) => "settings.view",
            _ => null
        };
    }
}
