using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CompeteDesk.Data;

/// <summary>
/// Lightweight Identity seeding for local/dev scenarios.
/// - Ensures the Admin role exists.
/// - Assigns Admin role to a seed user (config-driven) OR the first registered user.
/// </summary>
public static class IdentitySeeder
{
    public const string AdminRoleName = "Admin";
    public const string EditorRoleName = "Editor";
    public const string ReadOnlyRoleName = "ReadOnly";

    private static readonly string[] BaselineRoles = new[]
    {
        AdminRoleName,
        EditorRoleName,
        ReadOnlyRoleName
    };

    public static async Task EnsureAdminAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var config = services.GetRequiredService<IConfiguration>();

        // 1) Ensure baseline roles exist
        foreach (var role in BaselineRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // If someone is already an Admin, we're done.
        var anyAdmin = await userManager.GetUsersInRoleAsync(AdminRoleName);
        if (anyAdmin.Count > 0) return;

        // 2) Config-driven seed email (optional)
        // If set, that specific email becomes the default Admin.
        // This supports external logins (Google) where we don't have a password to pre-create the user.
        var seedEmail = config["AdminSeed:Email"];
        if (!string.IsNullOrWhiteSpace(seedEmail))
        {
            var seedUser = await userManager.FindByEmailAsync(seedEmail);
            if (seedUser is not null)
            {
                await userManager.AddToRoleAsync(seedUser, AdminRoleName);
            }

            // If the seed user doesn't exist yet, do NOT promote some other user.
            // ExternalLogin will assign Admin automatically when this email first signs in.
            return;
        }

        // 3) Fallback: promote the first registered user to Admin
        // This is MVP-friendly and avoids hard-coding credentials.
        var db = services.GetRequiredService<ApplicationDbContext>();
        var firstUser = await db.Users
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync();

        if (firstUser is not null)
        {
            await userManager.AddToRoleAsync(firstUser, AdminRoleName);
        }
    }

    /// <summary>
    /// Ensures that a signed-in user has at least one application role.
    /// Default role is Editor (so they can use CRUD features).
    /// </summary>
    public static async Task EnsureUserHasDefaultRoleAsync(IServiceProvider services, IdentityUser user)
    {
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        var roles = await userManager.GetRolesAsync(user);
        if (roles is not null && roles.Count > 0) return;

        await userManager.AddToRoleAsync(user, EditorRoleName);
    }
}
