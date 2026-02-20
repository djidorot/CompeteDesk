using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompeteDesk.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CompeteDesk.Services;

/// <summary>
/// Resolves the current "active" workspace for a user.
/// This mirrors the Dashboard behavior (querystring -> cookie -> latest),
/// so create flows can automatically attach records to the active workspace.
/// </summary>
public sealed class ActiveWorkspaceService
{
    public const string ActiveWorkspaceCookieName = "cd.activeWorkspaceId";

    private readonly ApplicationDbContext _db;

    public ActiveWorkspaceService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<int?> ResolveAsync(HttpContext http, string userId, int? workspaceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;

        // Priority: explicit querystring -> cookie -> latest
        int? activeId = null;
        if (workspaceId.HasValue && workspaceId.Value > 0)
        {
            activeId = workspaceId.Value;
        }
        else if (http.Request.Cookies.TryGetValue(ActiveWorkspaceCookieName, out var cookieVal)
                 && int.TryParse(cookieVal, out var parsedId)
                 && parsedId > 0)
        {
            activeId = parsedId;
        }

        if (activeId.HasValue)
        {
            var exists = await _db.Workspaces
                .AsNoTracking()
                .AnyAsync(w => w.Id == activeId.Value && w.OwnerId == userId, ct);
            if (exists) return activeId.Value;
        }

        // Fallback: latest workspace for this user
        var latest = await _db.Workspaces
            .AsNoTracking()
            .Where(w => w.OwnerId == userId)
            .OrderByDescending(w => w.UpdatedAtUtc ?? w.CreatedAtUtc)
            .Select(w => (int?)w.Id)
            .FirstOrDefaultAsync(ct);

        return latest;
    }

    public void PersistSelection(HttpContext http, int workspaceId)
    {
        if (workspaceId <= 0) return;

        http.Response.Cookies.Append(
            ActiveWorkspaceCookieName,
            workspaceId.ToString(),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(90),
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = http.Request.IsHttps
            });
    }
}
