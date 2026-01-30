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
    private const string AdminRoleName = "Admin";

    public static async Task EnsureAdminAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var config = services.GetRequiredService<IConfiguration>();

        // 1) Ensure role exists
        if (!await roleManager.RoleExistsAsync(AdminRoleName))
        {
            await roleManager.CreateAsync(new IdentityRole(AdminRoleName));
        }

        // If someone is already an Admin, we're done.
        var anyAdmin = await userManager.GetUsersInRoleAsync(AdminRoleName);
        if (anyAdmin.Count > 0) return;

        // 2) Config-driven seed user (optional)
        var seedEmail = config["AdminSeed:Email"];
        var seedPassword = config["AdminSeed:Password"];

        if (!string.IsNullOrWhiteSpace(seedEmail) && !string.IsNullOrWhiteSpace(seedPassword))
        {
            var seedUser = await userManager.FindByEmailAsync(seedEmail);
            if (seedUser is null)
            {
                seedUser = new IdentityUser
                {
                    UserName = seedEmail,
                    Email = seedEmail,
                    EmailConfirmed = true
                };

                var createRes = await userManager.CreateAsync(seedUser, seedPassword);
                if (!createRes.Succeeded)
                {
                    // If we can't create, fall through to "first user" behavior.
                    seedUser = null;
                }
            }

            if (seedUser is not null)
            {
                await userManager.AddToRoleAsync(seedUser, AdminRoleName);
                return;
            }
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
}
