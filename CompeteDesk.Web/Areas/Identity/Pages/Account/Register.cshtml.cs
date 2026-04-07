using System.ComponentModel.DataAnnotations;
using CompeteDesk.Data;
using CompeteDesk.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CompeteDesk.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IUserStore<IdentityUser> _userStore;
    private readonly IUserEmailStore<IdentityUser> _emailStore;
    private readonly ILogger<RegisterModel> _logger;
    private readonly ApplicationDbContext _db;

    public RegisterModel(
        UserManager<IdentityUser> userManager,
        IUserStore<IdentityUser> userStore,
        SignInManager<IdentityUser> signInManager,
        ApplicationDbContext db,
        ILogger<RegisterModel> logger)
    {
        _userManager = userManager;
        _userStore = userStore;
        _emailStore = GetEmailStore();
        _signInManager = signInManager;
        _db = db;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }
    public IList<AuthenticationScheme> ExternalLogins { get; set; } = [];

    public class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(GetPostLoginReturnUrl(returnUrl));
        }

        ReturnUrl = NormalizeReturnUrl(returnUrl);
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl = NormalizeReturnUrl(returnUrl ?? ReturnUrl);
        ReturnUrl = returnUrl;
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var normalizedEmail = NormalizeEmail(Input.Email);
        Input.Email = normalizedEmail;

        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail)
            ?? await _userManager.FindByNameAsync(normalizedEmail);

        if (existingUser is not null)
        {
            ModelState.AddModelError(string.Empty, "An account with this email already exists. Please log in instead.");
            return Page();
        }

        var user = CreateUser();
        await _userStore.SetUserNameAsync(user, normalizedEmail, CancellationToken.None);
        await _emailStore.SetEmailAsync(user, normalizedEmail, CancellationToken.None);
        user.EmailConfirmed = true;

        var result = await _userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        try
        {
            await IdentitySeeder.EnsureUserHasDefaultRoleAsync(HttpContext.RequestServices, user);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to assign the default role during registration for {Email}.", Input.Email);
        }

        await EnsureDefaultProfileAsync(user.Id);

        await _signInManager.SignInAsync(user, isPersistent: false);
        _logger.LogInformation("User created a new account with password.");
        _db.AuditLogs.Add(new AuditLog { OwnerId = user.Id, ActorUserId = user.Id, ActorEmail = user.Email, Action = "Register", EntityType = "Identity", EntityId = user.Id, Summary = "User created a new account.", CreatedAtUtc = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        return LocalRedirect(GetPostLoginReturnUrl(ReturnUrl));
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


    private async Task EnsureDefaultProfileAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var hasProfile = await _db.UserProfiles
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId);

        if (hasProfile)
        {
            return;
        }

        _db.UserProfiles.Add(new UserProfile
        {
            UserId = userId,
            PersonaRole = "Business Owner",
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    private static string NormalizeEmail(string? email)
    {
        return (email ?? string.Empty).Trim().ToLowerInvariant();
    }

    private IdentityUser CreateUser()
    {
        try
        {
            return Activator.CreateInstance<IdentityUser>();
        }
        catch
        {
            throw new InvalidOperationException($"Can't create an instance of '{nameof(IdentityUser)}'.");
        }
    }

    private IUserEmailStore<IdentityUser> GetEmailStore()
    {
        if (!_userManager.SupportsUserEmail)
        {
            throw new NotSupportedException("The default UI requires a user store with email support.");
        }

        return (IUserEmailStore<IdentityUser>)_userStore;
    }
}
