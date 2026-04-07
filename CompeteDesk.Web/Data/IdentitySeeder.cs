using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CompeteDesk.Data;

/// <summary>
/// Lightweight Identity seeding for local/dev scenarios.
/// - Ensures baseline roles exist.
/// - Assigns Admin role to an explicitly seeded local admin account OR the first registered user.
/// - Uses AdminSeed:Email together with AdminSeed:Password for explicit seeding.
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

        await EnsureBaselineRolesAsync(roleManager);

        var seedEmail = config["AdminSeed:Email"]?.Trim();
        var seedPassword = config["AdminSeed:Password"];
        var hasExplicitSeedAdmin = !string.IsNullOrWhiteSpace(seedEmail)
                                   && !string.IsNullOrWhiteSpace(seedPassword);

        if (hasExplicitSeedAdmin)
        {
            var seedUser = await userManager.FindByEmailAsync(seedEmail!);

            if (seedUser is null)
            {
                seedUser = new IdentityUser
                {
                    UserName = seedEmail,
                    Email = seedEmail,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(seedUser, seedPassword!);
                if (!createResult.Succeeded)
                {
                    throw new InvalidOperationException($"Unable to create configured admin seed user '{seedEmail}': {string.Join("; ", createResult.Errors.Select(e => e.Description))}");
                }
            }

            await EnsurePasswordAsync(userManager, seedUser, seedPassword!);
            await EnsureUserInRoleAsync(userManager, seedUser, UserRoleName);
            await EnsureUserInRoleAsync(userManager, seedUser, AdminRoleName);
            return;
        }

        var anyAdmin = await userManager.GetUsersInRoleAsync(AdminRoleName);
        if (anyAdmin.Count > 0) return;

        // Fallback: promote the first registered user to Admin.
        var db = services.GetRequiredService<ApplicationDbContext>();
        var firstUser = await db.Users
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync();

        if (firstUser is not null)
        {
            await EnsureUserInRoleAsync(userManager, firstUser, UserRoleName);
            await EnsureUserInRoleAsync(userManager, firstUser, AdminRoleName);
        }
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
            // Corrupted legacy password hash in the DB. Reset it below.
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
