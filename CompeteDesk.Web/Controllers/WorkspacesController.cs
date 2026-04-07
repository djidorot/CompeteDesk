using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Models;
using CompeteDesk.ViewModels.Workspaces;
using CompeteDesk.Models.Common;
using CompeteDesk.Services.Notifications;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace CompeteDesk.Controllers;

[Authorize]
public class WorkspacesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly InAppNotificationService _notifications;

    public WorkspacesController(ApplicationDbContext db, UserManager<IdentityUser> userManager, InAppNotificationService notifications)
    {
        _db = db;
        _userManager = userManager;
        _notifications = notifications;
    }

    private async Task<IdentityUser?> GetUserAsync() => await _userManager.GetUserAsync(User);

    private async Task<string> GetUserIdAsync()
    {
        var user = await GetUserAsync();
        return user?.Id ?? string.Empty;
    }

    // GET: /Workspaces
    public async Task<IActionResult> Index(int page = 1, int pageSize = 25, bool partial = false, CancellationToken ct = default)
    {
        ViewData["Title"] = "Workspaces";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var memberWorkspaceIds = _db.WorkspaceMembers
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.WorkspaceId);

        var query = _db.Workspaces
            .AsNoTracking()
            .Where(x => x.OwnerId == userId || memberWorkspaceIds.Contains(x.Id))
            .OrderByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
            .ThenBy(x => x.Name);

        var paged = await PagedResult<Workspace>.CreateAsync(query, page, pageSize, ct);

        ViewBag.Page = paged.Page;
        ViewBag.PageSize = paged.PageSize;
        ViewBag.TotalCount = paged.TotalCount;
        ViewBag.TotalPages = paged.TotalPages;

        if (partial || Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView("_WorkspacesList", paged.Items.ToList());
        return View(paged.Items.ToList());
    }

    // GET: /Workspaces/Create
    public IActionResult Create()
    {
        ViewData["Title"] = "New Workspace";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        return View(new CreateWorkspaceViewModel());
    }

    // POST: /Workspaces/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateWorkspaceViewModel vm)
    {
        ViewData["Title"] = "New Workspace";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var name = (vm.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(vm.Name), "Workspace name is required.");
            return View(vm);
        }

        var exists = await _db.Workspaces.AnyAsync(x => x.OwnerId == userId && x.Name == name);
        if (exists)
        {
            ModelState.AddModelError(nameof(vm.Name), "You already have a workspace with that name.");
            return View(vm);
        }

        var me = await GetUserAsync();
        var workspace = new Workspace
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim(),
            OwnerId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Workspaces.Add(workspace);
        await _db.SaveChangesAsync();

        _db.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = workspace.Id,
            UserId = userId,
            UserEmail = me?.Email,
            Role = "Owner",
            JoinedAtUtc = DateTime.UtcNow,
            InvitedByUserId = userId
        });
        await _db.SaveChangesAsync();

        TempData["ToastSuccess"] = "Workspace created.";
        return RedirectToAction(nameof(Details), new { id = workspace.Id });
    }

    // GET: /Workspaces/Details/5
    public async Task<IActionResult> Details(int id, CancellationToken ct = default)
    {
        ViewData["Title"] = "Workspace Details";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var workspace = await _db.Workspaces.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (workspace == null) return NotFound();

        var isAllowed = workspace.OwnerId == userId || await _db.WorkspaceMembers.AnyAsync(x => x.WorkspaceId == id && x.UserId == userId, ct);
        if (!isAllowed) return Forbid();

        var memberRows = await _db.WorkspaceMembers
            .AsNoTracking()
            .Where(x => x.WorkspaceId == id)
            .OrderBy(x => x.Role)
            .ThenBy(x => x.UserEmail)
            .Select(x => new WorkspaceMemberRow
            {
                Id = x.Id,
                UserId = x.UserId,
                Email = x.UserEmail ?? x.UserId,
                Role = x.Role,
                JoinedAtUtc = x.JoinedAtUtc,
                IsOwner = x.Role == "Owner"
            })
            .ToListAsync(ct);

        var inviteRows = await _db.WorkspaceInvites
            .AsNoTracking()
            .Where(x => x.WorkspaceId == id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new WorkspaceInviteRow
            {
                Id = x.Id,
                Email = x.Email,
                Role = x.Role,
                Status = x.Status,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(ct);

        var commentCounts = await _db.StrategyComments
            .AsNoTracking()
            .Where(x => x.WorkspaceId == id)
            .GroupBy(x => x.StrategyId)
            .Select(x => new { StrategyId = x.Key, Count = x.Count() })
            .ToListAsync(ct);

        var strategies = await _db.Strategies
            .AsNoTracking()
            .Where(x => x.WorkspaceId == id)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .Select(x => new WorkspaceStrategyRow
            {
                Id = x.Id,
                Name = x.Name,
                Category = x.Category,
                Status = x.Status,
                CommentCount = 0
            })
            .ToListAsync(ct);

        foreach (var strategy in strategies)
        {
            strategy.CommentCount = commentCounts.FirstOrDefault(x => x.StrategyId == strategy.Id)?.Count ?? 0;
        }

        var activity = await _db.AuditLogs
            .AsNoTracking()
            .Where(x => x.OwnerId == workspace.OwnerId && (x.EntityType == "Workspace" || x.EntityType == "Strategy" || x.EntityType == "ActionItem"))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(10)
            .Select(x => new WorkspaceActivityRow
            {
                CreatedAtUtc = x.CreatedAtUtc,
                Action = x.Action,
                Summary = x.Summary,
                ActorEmail = x.ActorEmail
            })
            .ToListAsync(ct);

        var vm = new WorkspaceDetailsViewModel
        {
            Workspace = workspace,
            Members = memberRows,
            Invites = inviteRows,
            Strategies = strategies,
            Activity = activity,
            StrategyCount = strategies.Count,
            ActiveStrategyCount = strategies.Count(x => x.Status == "Active"),
            CommentCount = strategies.Sum(x => x.CommentCount)
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(int workspaceId, string email, string role, CancellationToken ct = default)
    {
        var me = await GetUserAsync();
        var userId = me?.Id ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var workspace = await _db.Workspaces.FirstOrDefaultAsync(x => x.Id == workspaceId && x.OwnerId == userId, ct);
        if (workspace == null) return NotFound();

        email = (email ?? string.Empty).Trim();
        role = NormalizeRole(role);
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["ToastError"] = "Email is required.";
            return RedirectToAction(nameof(Details), new { id = workspaceId });
        }

        var existingMember = await _db.WorkspaceMembers.AnyAsync(x => x.WorkspaceId == workspaceId && x.UserEmail == email, ct);
        if (existingMember)
        {
            TempData["ToastError"] = "That user is already a workspace member.";
            return RedirectToAction(nameof(Details), new { id = workspaceId });
        }

        var existingInvite = await _db.WorkspaceInvites.FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Email == email && x.Status == "Pending", ct);
        if (existingInvite != null)
        {
            existingInvite.Role = role;
            existingInvite.CreatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            _db.WorkspaceInvites.Add(new WorkspaceInvite
            {
                WorkspaceId = workspaceId,
                Email = email,
                Role = role,
                Status = "Pending",
                InvitedByUserId = userId,
                InvitedByEmail = me?.Email,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
        var invitedUser = await _userManager.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Email == email, ct);
        if (invitedUser != null)
        {
            await _notifications.CreateAsync(invitedUser.Id, "Workspace invitation", $"You were invited to join {workspace.Name} as {role}.", "Workspace", "/Workspaces");
        }
        TempData["ToastSuccess"] = "Workspace invite saved.";
        return RedirectToAction(nameof(Details), new { id = workspaceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMemberRole(int workspaceId, int memberId, string role, CancellationToken ct = default)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var workspace = await _db.Workspaces.AsNoTracking().FirstOrDefaultAsync(x => x.Id == workspaceId && x.OwnerId == userId, ct);
        if (workspace == null) return NotFound();

        var member = await _db.WorkspaceMembers.FirstOrDefaultAsync(x => x.Id == memberId && x.WorkspaceId == workspaceId, ct);
        if (member == null) return NotFound();
        if (member.Role == "Owner")
        {
            TempData["ToastError"] = "Owner role cannot be changed here.";
            return RedirectToAction(nameof(Details), new { id = workspaceId });
        }

        member.Role = NormalizeRole(role);
        await _db.SaveChangesAsync(ct);
        TempData["ToastSuccess"] = "Member role updated.";
        return RedirectToAction(nameof(Details), new { id = workspaceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(int workspaceId, int memberId, CancellationToken ct = default)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var workspace = await _db.Workspaces.AsNoTracking().FirstOrDefaultAsync(x => x.Id == workspaceId && x.OwnerId == userId, ct);
        if (workspace == null) return NotFound();

        var member = await _db.WorkspaceMembers.FirstOrDefaultAsync(x => x.Id == memberId && x.WorkspaceId == workspaceId, ct);
        if (member == null) return NotFound();
        if (member.Role == "Owner")
        {
            TempData["ToastError"] = "Owner cannot be removed here.";
            return RedirectToAction(nameof(Details), new { id = workspaceId });
        }

        _db.WorkspaceMembers.Remove(member);
        await _db.SaveChangesAsync(ct);
        TempData["ToastSuccess"] = "Member removed.";
        return RedirectToAction(nameof(Details), new { id = workspaceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptInvite(int inviteId, CancellationToken ct = default)
    {
        var me = await GetUserAsync();
        var userId = me?.Id ?? string.Empty;
        var email = me?.Email ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var invite = await _db.WorkspaceInvites.FirstOrDefaultAsync(x => x.Id == inviteId && x.Status == "Pending", ct);
        if (invite == null) return NotFound();
        if (!string.Equals(invite.Email, email, StringComparison.OrdinalIgnoreCase)) return Forbid();

        var exists = await _db.WorkspaceMembers.AnyAsync(x => x.WorkspaceId == invite.WorkspaceId && x.UserId == userId, ct);
        if (!exists)
        {
            _db.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = invite.WorkspaceId,
                UserId = userId,
                UserEmail = email,
                Role = NormalizeRole(invite.Role),
                JoinedAtUtc = DateTime.UtcNow,
                InvitedByUserId = invite.InvitedByUserId
            });
        }

        invite.Status = "Accepted";
        invite.AcceptedAtUtc = DateTime.UtcNow;
        invite.AcceptedByUserId = userId;

        await _db.SaveChangesAsync(ct);
        await _notifications.CreateAsync(invite.InvitedByUserId, "Workspace invitation accepted", $"{email} accepted the invitation for workspace #{invite.WorkspaceId}.", "Workspace", $"/Workspaces/Details/{invite.WorkspaceId}");
        TempData["ToastSuccess"] = "Workspace invite accepted.";
        return RedirectToAction(nameof(Details), new { id = invite.WorkspaceId });
    }

    private static string NormalizeRole(string? role)
    {
        return (role ?? string.Empty).Trim() switch
        {
            "Owner" => "Owner",
            "Editor" => "Editor",
            _ => "Viewer"
        };
    }
}
