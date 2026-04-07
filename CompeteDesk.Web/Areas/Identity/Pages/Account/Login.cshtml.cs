using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CompeteDesk.Data;
using CompeteDesk.Models;

namespace CompeteDesk.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<LoginModel> _logger;
    private readonly ApplicationDbContext _db;

    public LoginModel(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        ApplicationDbContext db,
        ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _db = db;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IList<AuthenticationScheme>? ExternalLogins { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(GetPostLoginReturnUrl(returnUrl));
        }

        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        returnUrl = NormalizeReturnUrl(returnUrl);

        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        ReturnUrl = returnUrl;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl = NormalizeReturnUrl(returnUrl);
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var normalizedEmail = NormalizeEmail(Input.Email);
        Input.Email = normalizedEmail;

        var user = await _userManager.FindByEmailAsync(normalizedEmail)
                   ?? await _userManager.FindByNameAsync(normalizedEmail);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return Page();
        }

        Microsoft.AspNetCore.Identity.SignInResult result;

        try
        {
            result = await _signInManager.PasswordSignInAsync(
                user,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure: false);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Invalid password hash detected for user {Email}. Resetting password hash.", Input.Email);

            user.PasswordHash = _userManager.PasswordHasher.HashPassword(user, Input.Password);
            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Unable to repair this account automatically.");
                return Page();
            }

            result = await _signInManager.PasswordSignInAsync(
                user,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure: false);
        }

        if (result.Succeeded)
        {
            _logger.LogInformation("User logged in.");
            _db.AuditLogs.Add(new AuditLog { OwnerId = user.Id, ActorUserId = user.Id, ActorEmail = user.Email, Action = "Login", EntityType = "Identity", EntityId = user.Id, Summary = "User logged in.", CreatedAtUtc = DateTime.UtcNow });
            await _db.SaveChangesAsync();
            return LocalRedirect(GetPostLoginReturnUrl(returnUrl));
        }

        if (result.RequiresTwoFactor)
        {
            return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, Input.RememberMe });
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User account locked out.");
            return RedirectToPage("./Lockout");
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return Page();
    }

    private static string NormalizeEmail(string? email)
    {
        return (email ?? string.Empty).Trim().ToLowerInvariant();
    }

    private string NormalizeReturnUrl(string? returnUrl)
    {
        var homeUrl = Url.Content("~/");

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return homeUrl;
        }

        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out _))
        {
            return homeUrl;
        }

        if (!Url.IsLocalUrl(returnUrl))
        {
            return homeUrl;
        }

        var normalized = returnUrl.Trim();
        var pathOnly = normalized.Split('?', '#')[0];

        if (pathOnly.StartsWith("~/", StringComparison.Ordinal))
        {
            pathOnly = "/" + pathOnly[2..];
        }

        if (string.Equals(pathOnly, "/Identity/Account/Login", StringComparison.OrdinalIgnoreCase)
            || string.Equals(pathOnly, "/Identity/Account/Register", StringComparison.OrdinalIgnoreCase)
            || string.Equals(pathOnly, "/Identity/Account/Logout", StringComparison.OrdinalIgnoreCase))
        {
            return homeUrl;
        }

        return normalized;
    }

    private string GetPostLoginReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || returnUrl == "/" || returnUrl == "~/" || returnUrl == Url.Content("~/"))
        {
            return "/Dashboard";
        }

        return returnUrl;
    }
}