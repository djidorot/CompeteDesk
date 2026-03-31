using CompeteDesk.Data;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CompeteDesk.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ExternalLoginModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IUserStore<IdentityUser> _userStore;
    private readonly IUserEmailStore<IdentityUser> _emailStore;
    private readonly IConfiguration _config;
    private readonly ILogger<ExternalLoginModel> _logger;

    public ExternalLoginModel(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IUserStore<IdentityUser> userStore,
        IConfiguration config,
        ILogger<ExternalLoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _userStore = userStore;
        _emailStore = GetEmailStore();
        _config = config;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ProviderDisplayName { get; set; }
    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public IActionResult OnGet() => RedirectToPage("./Login");

    public IActionResult OnPost(string provider, string? returnUrl = null)
    {
        // Request a redirect to the external login provider.
        var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return new ChallengeResult(provider, properties);
    }


    public async Task<IActionResult> OnGetCallbackAsync(string? returnUrl = null, string? remoteError = null)
    {
        returnUrl ??= Url.Content("~/");
        ReturnUrl = returnUrl;

        if (remoteError != null)
        {
            ErrorMessage = $"Error from external provider: {remoteError}";
            return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            ErrorMessage = "Error loading external login information.";
            return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
        }

        // 1) If the external login is already linked, sign in.
        var signInResult = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: false,
            bypassTwoFactor: true);

        if (signInResult.Succeeded)
        {
            _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity?.Name, info.LoginProvider);

            // Make the configured seed email the default Admin, even when signing in via Google.
            var linkedUser = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (linkedUser is not null)
            {
                var linkedEmail = await _userManager.GetEmailAsync(linkedUser);
                await MaybeAssignSeedAdminAsync(linkedUser, linkedEmail);
            }

            return LocalRedirect(GetPostLoginReturnUrl(returnUrl));
        }

        if (signInResult.IsLockedOut)
        {
            return RedirectToPage("./Lockout");
        }

        // 2) If we have an email and a local user already exists, AUTO-LINK and sign in.
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (!string.IsNullOrWhiteSpace(email))
        {
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
                if (addLoginResult.Succeeded)
                {
                    await _signInManager.SignInAsync(existingUser, isPersistent: false);
                    _logger.LogInformation("Linked {LoginProvider} login for existing user {Email}.", info.LoginProvider, email);

                    await MaybeAssignSeedAdminAsync(existingUser, email);
                    await IdentitySeeder.EnsureUserHasDefaultRoleAsync(HttpContext.RequestServices, existingUser);

                    return LocalRedirect(GetPostLoginReturnUrl(returnUrl));
                }

                // If linking failed because it's already linked elsewhere, surface a friendly error.
                foreach (var e in addLoginResult.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
            }


            // If user doesn't exist yet, AUTO-CREATE and sign in (skip the "associate account" screen).
            if (existingUser == null)
            {
                var newUser = CreateUser();
                await _userStore.SetUserNameAsync(newUser, email, CancellationToken.None);
                await _emailStore.SetEmailAsync(newUser, email, CancellationToken.None);

                // Treat externally authenticated emails as confirmed to avoid RequireConfirmedAccount blocking sign-in.
                newUser.EmailConfirmed = true;

                var createRes = await _userManager.CreateAsync(newUser);
                if (createRes.Succeeded)
                {
                    var addLoginRes = await _userManager.AddLoginAsync(newUser, info);
                    if (addLoginRes.Succeeded)
                    {
                        await MaybeAssignSeedAdminAsync(newUser, email);
                        await IdentitySeeder.EnsureUserHasDefaultRoleAsync(HttpContext.RequestServices, newUser);

                        await _signInManager.SignInAsync(newUser, isPersistent: false, info.LoginProvider);
                        _logger.LogInformation("Auto-created local user for external login {Email} via {Provider}.", email, info.LoginProvider);

                        return LocalRedirect(GetPostLoginReturnUrl(returnUrl));
                    }

                    // If linking failed, roll back the user to avoid orphan accounts.
                    await _userManager.DeleteAsync(newUser);
                    foreach (var err in addLoginRes.Errors)
                        ModelState.AddModelError(string.Empty, err.Description);
                }
                else
                {
                    foreach (var err in createRes.Errors)
                        ModelState.AddModelError(string.Empty, err.Description);
                }
            }

            // Pre-fill email for the confirmation form.
            Input = new InputModel { Email = email };
        }

        ProviderDisplayName = info.ProviderDisplayName;
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmationAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ReturnUrl = returnUrl;

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            ErrorMessage = "Error loading external login information during confirmation.";
            return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
        }

        if (!ModelState.IsValid)
        {
            ProviderDisplayName = info.ProviderDisplayName;
            return Page();
        }

        // If a user with this email already exists, link the external login and sign them in
        // instead of showing 'username already taken'.
        var existingUser = await _userManager.FindByEmailAsync(Input.Email);
        if (existingUser != null)
        {
            var addLoginRes = await _userManager.AddLoginAsync(existingUser, info);
            if (addLoginRes.Succeeded)
            {
                await _signInManager.SignInAsync(existingUser, isPersistent: false);
                _logger.LogInformation("Linked {LoginProvider} login via confirmation for existing user {Email}.", info.LoginProvider, Input.Email);

                await MaybeAssignSeedAdminAsync(existingUser, Input.Email);
                await IdentitySeeder.EnsureUserHasDefaultRoleAsync(HttpContext.RequestServices, existingUser);

                return LocalRedirect(GetPostLoginReturnUrl(returnUrl));
            }

            foreach (var e in addLoginRes.Errors)
                ModelState.AddModelError(string.Empty, e.Description);

            ProviderDisplayName = info.ProviderDisplayName;
            return Page();
        }

        // Otherwise create a new user and link the login.
        var user = CreateUser();

        await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
        await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

        // Treat externally authenticated emails as confirmed to avoid RequireConfirmedAccount blocking sign-in.
        user.EmailConfirmed = true;

        var createResult = await _userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            ProviderDisplayName = info.ProviderDisplayName;
            return Page();
        }

        var addLoginResult2 = await _userManager.AddLoginAsync(user, info);
        if (!addLoginResult2.Succeeded)
        {
            foreach (var error in addLoginResult2.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            ProviderDisplayName = info.ProviderDisplayName;
            return Page();
        }

        _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);

        await MaybeAssignSeedAdminAsync(user, Input.Email);
        await IdentitySeeder.EnsureUserHasDefaultRoleAsync(HttpContext.RequestServices, user);

        await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
        return LocalRedirect(GetPostLoginReturnUrl(returnUrl));
    }

    private async Task MaybeAssignSeedAdminAsync(IdentityUser user, string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return;

        var seedEmail = _config["AdminSeed:Email"];
        if (string.IsNullOrWhiteSpace(seedEmail)) return;

        if (!string.Equals(email.Trim(), seedEmail.Trim(), StringComparison.OrdinalIgnoreCase)) return;

        const string adminRole = "Admin";
        try
        {
            if (!await _roleManager.RoleExistsAsync(adminRole))
            {
                await _roleManager.CreateAsync(new IdentityRole(adminRole));
            }

            if (!await _userManager.IsInRoleAsync(user, adminRole))
            {
                var res = await _userManager.AddToRoleAsync(user, adminRole);
                if (res.Succeeded)
                {
                    _logger.LogInformation("Assigned {Role} role to seed admin email {Email}.", adminRole, email);
                }
                else
                {
                    _logger.LogWarning("Failed assigning {Role} role to {Email}: {Errors}", adminRole, email, string.Join("; ", res.Errors.Select(e => e.Code)));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while assigning seed admin role for {Email}.", email);
        }
    }

    private string GetPostLoginReturnUrl(string? returnUrl)
    {
        // If no specific return target, send users to the Dashboard by default.
        if (string.IsNullOrWhiteSpace(returnUrl)) return "/Dashboard";

        // Identity UI commonly uses "/" or "~/" for home; we want Dashboard after login.
        if (returnUrl == "/" || returnUrl == "~/" || returnUrl == Url.Content("~/"))
            return "/Dashboard";

        return returnUrl;
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
