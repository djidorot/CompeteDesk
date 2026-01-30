using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CompeteDesk.Data;
using CompeteDesk.Models;
using CompeteDesk.ViewModels.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompeteDesk.Controllers;

[Authorize]
public class OnboardingController : Controller
{
    private readonly ApplicationDbContext _db;

    public OnboardingController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var existing = await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
        if (existing != null)
        {
            // Already onboarded.
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new OnboardingViewModel());
    }

    [HttpGet]
    public IActionResult Skip(string? returnUrl = null)
    {
        // Set a cookie so the onboarding gate lets the user continue.
        Response.Cookies.Append("cd_onboarding_skipped", "1", new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps
        });

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(OnboardingViewModel vm)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        // Basic validation
        if (!ModelState.IsValid)
            return View(vm);

        // Ensure role is one of the supported options.
        var allowed = OnboardingViewModel.Roles.Select(r => r.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!allowed.Contains(vm.PersonaRole))
        {
            ModelState.AddModelError(nameof(vm.PersonaRole), "Please choose a valid role.");
            return View(vm);
        }

        var existing = await _db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (existing == null)
        {
            existing = new UserProfile
            {
                UserId = userId,
                PersonaRole = vm.PersonaRole,
                PrimaryGoal = string.IsNullOrWhiteSpace(vm.PrimaryGoal) ? null : vm.PrimaryGoal.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.UserProfiles.Add(existing);
        }
        else
        {
            existing.PersonaRole = vm.PersonaRole;
            existing.PrimaryGoal = string.IsNullOrWhiteSpace(vm.PrimaryGoal) ? null : vm.PrimaryGoal.Trim();
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        // User completed onboarding; remove any skip cookie.
        Response.Cookies.Delete("cd_onboarding_skipped");

        return RedirectToAction("Index", "Dashboard");
    }
}
