using CompeteDesk.Data;
using CompeteDesk.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CompeteDesk.Services.Security;

public sealed class FeaturePermissionService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public FeaturePermissionService(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public static IReadOnlyList<FeaturePermissionDefinition> Catalog { get; } =
    [
        new("dashboard.view", "Dashboard", "View dashboard and overview pages", "Dashboard"),
        new("dashboard.manage", "Dashboard", "Run dashboard actions and setup flows", "Dashboard"),
        new("workspaces.view", "Workspaces", "View workspaces and details", "Execution"),
        new("workspaces.manage", "Workspaces", "Create workspaces, invite members, and update settings", "Execution"),
        new("strategies.view", "Strategies", "View strategy records", "Execution"),
        new("strategies.manage", "Strategies", "Create, edit, and delete strategies", "Execution"),
        new("actions.view", "Actions", "View action plans", "Execution"),
        new("actions.manage", "Actions", "Create, edit, and delete actions", "Execution"),
        new("habits.view", "Habits", "View habits and check-ins", "Execution"),
        new("habits.manage", "Habits", "Create and update habits", "Execution"),
        new("metrics.view", "Metrics", "View metrics and momentum", "Execution"),
        new("metrics.manage", "Metrics", "Create and update metrics entries", "Execution"),
        new("recommendations.view", "Recommendations", "View AI recommendations", "Intelligence"),
        new("recommendations.manage", "Recommendations", "Create actions from recommendations", "Intelligence"),
        new("study-planner.view", "Study Planner", "View study planner", "Planning"),
        new("study-planner.manage", "Study Planner", "Create and update study plans", "Planning"),
        new("exports.view", "Exports", "Export progress and summary reports", "Planning"),
        new("website-analysis.view", "Website Analysis", "View website analysis reports", "Intelligence"),
        new("website-analysis.manage", "Website Analysis", "Run website analyses", "Intelligence"),
        new("business-analysis.view", "Business Analysis", "View business analysis reports", "Intelligence"),
        new("business-analysis.manage", "Business Analysis", "Generate and update business analyses", "Intelligence"),
        new("war-room.view", "War Room", "View war room intelligence", "Intelligence"),
        new("war-room.manage", "War Room", "Create and update war room intelligence", "Intelligence"),
        new("activity.view", "Activity", "View activity logs", "Admin & Audit"),
        new("settings.view", "Settings", "View settings", "General")
    ];

    public async Task<bool> HasPermissionAsync(IdentityUser user, string permissionKey, CancellationToken ct = default)
    {
        var explicitOverride = await _db.UserFeaturePermissions
            .AsNoTracking()
            .Where(x => x.UserId == user.Id && x.PermissionKey == permissionKey)
            .Select(x => (bool?)x.IsGranted)
            .FirstOrDefaultAsync(ct);

        if (explicitOverride.HasValue)
        {
            return explicitOverride.Value;
        }

        var roles = await _userManager.GetRolesAsync(user);
        return ResolveFromRoles(roles, permissionKey);
    }

    public async Task<Dictionary<string, bool>> GetEffectivePermissionsAsync(IdentityUser user, CancellationToken ct = default)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var explicitPermissions = await _db.UserFeaturePermissions
            .AsNoTracking()
            .Where(x => x.UserId == user.Id)
            .ToDictionaryAsync(x => x.PermissionKey, x => x.IsGranted, ct);

        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Catalog)
        {
            result[item.Key] = explicitPermissions.TryGetValue(item.Key, out var granted)
                ? granted
                : ResolveFromRoles(roles, item.Key);
        }

        return result;
    }

    public async Task SavePermissionsAsync(string userId, IEnumerable<string> grantedPermissionKeys, CancellationToken ct = default)
    {
        var selected = new HashSet<string>(grantedPermissionKeys ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var existing = await _db.UserFeaturePermissions.Where(x => x.UserId == userId).ToListAsync(ct);

        _db.UserFeaturePermissions.RemoveRange(existing);

        var rows = Catalog
            .Select(x => new UserFeaturePermission
            {
                UserId = userId,
                PermissionKey = x.Key,
                IsGranted = selected.Contains(x.Key),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

        await _db.UserFeaturePermissions.AddRangeAsync(rows, ct);
        await _db.SaveChangesAsync(ct);
    }

    private static bool ResolveFromRoles(IEnumerable<string> roles, string permissionKey)
    {
        var roleSet = new HashSet<string>(roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        if (roleSet.Contains(IdentitySeeder.AdminRoleName))
        {
            return true;
        }

        if (roleSet.Contains(IdentitySeeder.ReadOnlyRoleName))
        {
            return permissionKey.EndsWith(".view", StringComparison.OrdinalIgnoreCase);
        }

        if (roleSet.Contains(IdentitySeeder.EditorRoleName))
        {
            return !permissionKey.StartsWith("admin.", StringComparison.OrdinalIgnoreCase);
        }

        // Default application user: can use normal product features, but not audit/admin-only screens.
        return !permissionKey.StartsWith("activity.", StringComparison.OrdinalIgnoreCase)
               && !permissionKey.StartsWith("admin.", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record FeaturePermissionDefinition(string Key, string FeatureName, string Description, string Group);
