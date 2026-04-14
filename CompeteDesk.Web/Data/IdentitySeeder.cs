using Microsoft.AspNetCore.Identity;

namespace CompeteDesk.Data;

/// <summary>
/// Identity seeding with production-safe defaults.
/// - Ensures baseline roles exist.
/// - Seeds an admin only when explicit seed credentials are configured.
/// - Skips automatic admin creation/promotion in production unless explicitly allowed.
/// </summary>
public static class IdentitySeeder
{
    public const string UserRoleName = "User";
    public const string AdminRoleName = "Admin";
    public const string EditorRoleName = "Editor";
    public const string ReadOnlyRoleName = "ReadOnly";

    private static readonly string[] BaselineRoles =
    [
        UserRoleName,
        AdminRoleName,
        EditorRoleName,
        ReadOnlyRoleName
    ];

    public static async Task EnsureAdminAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var config = services.GetRequiredService<IConfiguration>();
        var environment = services.GetRequiredService<IWebHostEnvironment>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

        await EnsureBaselineRolesAsync(roleManager);

        var seedEmail = config["AdminSeed:Email"]?.Trim();
        var seedPassword = config["AdminSeed:Password"];
        var allowInProduction = config.GetValue<bool>("AdminSeed:AllowInProduction");
        var hasExplicitSeedAdmin = !string.IsNullOrWhiteSpace(seedEmail)
                                   && !string.IsNullOrWhiteSpace(seedPassword);

        if (!hasExplicitSeedAdmin)
        {
            logger.LogInformation("No explicit admin seed credentials configured. Skipping admin seeding.");
            return;
        }

        if (!environment.IsDevelopment() && !allowInProduction)
        {
            logger.LogWarning(
                "Admin seed credentials were provided, but admin seeding is disabled outside Development unless AdminSeed:AllowInProduction=true.");
            return;
        }

        var normalizedSeedEmail = seedEmail!;
        var seedUser = await userManager.FindByEmailAsync(normalizedSeedEmail)
            ?? await userManager.FindByNameAsync(normalizedSeedEmail);

        if (seedUser is null)
        {
            seedUser = new IdentityUser
            {
                UserName = normalizedSeedEmail,
                Email = normalizedSeedEmail,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(seedUser, seedPassword!);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException($"Unable to create configured admin seed user '{normalizedSeedEmail}': {string.Join("; ", createResult.Errors.Select(e => e.Description))}");
            }
        }
        else
        {
            var needsUpdate = false;

            if (!string.Equals(seedUser.Email, normalizedSeedEmail, StringComparison.OrdinalIgnoreCase))
            {
                seedUser.Email = normalizedSeedEmail;
                needsUpdate = true;
            }

            if (!string.Equals(seedUser.UserName, normalizedSeedEmail, StringComparison.OrdinalIgnoreCase))
            {
                seedUser.UserName = normalizedSeedEmail;
                needsUpdate = true;
            }

            if (!seedUser.EmailConfirmed)
            {
                seedUser.EmailConfirmed = true;
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                var updateResult = await userManager.UpdateAsync(seedUser);
                if (!updateResult.Succeeded)
                {
                    throw new InvalidOperationException($"Unable to update configured admin seed user '{normalizedSeedEmail}': {string.Join("; ", updateResult.Errors.Select(e => e.Description))}");
                }
            }
        }

        await EnsurePasswordAsync(userManager, seedUser, seedPassword!);
        await EnsureUserInRoleAsync(userManager, seedUser, UserRoleName);
        await EnsureUserInRoleAsync(userManager, seedUser, AdminRoleName);
    }

    public static async Task EnsureUserHasDefaultRoleAsync(IServiceProvider services, IdentityUser user)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        await EnsureBaselineRolesAsync(roleManager);

        var roles = await userManager.GetRolesAsync(user);
        if (roles is not null && roles.Count > 0) return;

        await EnsureUserInRoleAsync(userManager, user, UserRoleName);
    }

    public static async Task EnsurePasswordAsync(UserManager<IdentityUser> userManager, IdentityUser user, string password)
    {
        if (string.IsNullOrWhiteSpace(password)) return;

        var hasPassword = false;
        try
        {
            hasPassword = await userManager.HasPasswordAsync(user);
        }
        catch (FormatException)
        {
            hasPassword = true;
        }

        IdentityResult result;
        if (!hasPassword)
        {
            result = await userManager.AddPasswordAsync(user, password);
        }
        else
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            result = await userManager.ResetPasswordAsync(user, resetToken, password);
        }

        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Unable to set password for '{user.Email ?? user.UserName}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }

    private static async Task EnsureBaselineRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in BaselineRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException($"Unable to create role '{role}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
                }
            }
        }
    }

    private static async Task EnsureUserInRoleAsync(UserManager<IdentityUser> userManager, IdentityUser user, string role)
    {
        if (!await userManager.IsInRoleAsync(user, role))
        {
            var result = await userManager.AddToRoleAsync(user, role);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Unable to add '{user.Email ?? user.UserName}' to role '{role}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
            }
        }
    }
}
