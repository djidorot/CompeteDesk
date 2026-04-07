using CompeteDesk.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompeteDesk.Controllers;

[Authorize]
public sealed class NotificationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public NotificationsController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private async Task<string?> GetUserIdAsync() => (await _userManager.GetUserAsync(User))?.Id;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var items = await _db.Notifications
            .AsNoTracking()
            .Where(x => x.OwnerId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .ToListAsync(ct);

        ViewData["Title"] = "Notifications";
        ViewData["UseSidebar"] = true;
        ViewData["LayoutFluid"] = true;
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, string? returnUrl = null, CancellationToken ct = default)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        var item = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId, ct);
        if (item != null && !item.IsRead)
        {
            item.IsRead = true;
            item.ReadAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        var items = await _db.Notifications.Where(x => x.OwnerId == userId && !x.IsRead).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            item.IsRead = true;
            item.ReadAtUtc = now;
        }
        await _db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Index));
    }
}
