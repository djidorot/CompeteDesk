using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompeteDesk.Data;
using CompeteDesk.Models;
using Microsoft.EntityFrameworkCore;

namespace CompeteDesk.Services;

public sealed class WorkspaceAccessService
{
    private readonly ApplicationDbContext _db;

    public WorkspaceAccessService(ApplicationDbContext db)
    {
        _db = db;
    }

    public IQueryable<int> AccessibleWorkspaceIds(string userId)
        => _db.Workspaces
            .AsNoTracking()
            .Where(w => w.OwnerId == userId || _db.WorkspaceMembers.Any(m => m.WorkspaceId == w.Id && m.UserId == userId))
            .Select(w => w.Id);

    public IQueryable<Workspace> AccessibleWorkspaces(string userId)
        => _db.Workspaces
            .AsNoTracking()
            .Where(w => w.OwnerId == userId || _db.WorkspaceMembers.Any(m => m.WorkspaceId == w.Id && m.UserId == userId));

    public async Task<List<Workspace>> ListAccessibleWorkspacesAsync(string userId, CancellationToken ct = default)
        => await AccessibleWorkspaces(userId)
            .OrderByDescending(w => w.UpdatedAtUtc ?? w.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<Workspace?> GetAccessibleWorkspaceAsync(string userId, int workspaceId, CancellationToken ct = default)
        => await AccessibleWorkspaces(userId).FirstOrDefaultAsync(w => w.Id == workspaceId, ct);

    public async Task<bool> CanAccessWorkspaceAsync(string userId, int workspaceId, CancellationToken ct = default)
        => await AccessibleWorkspaceIds(userId).AnyAsync(id => id == workspaceId, ct);
}
