using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Services.Gemini;
using CompeteDesk.Services.OpenAI;
using CompeteDesk.Services.WebsiteAnalysis;
using CompeteDesk.Services.BusinessAnalysis;
using CompeteDesk.Services.WarRoom;
using CompeteDesk.Services.Ai;
using CompeteDesk.Services.Habits;
using CompeteDesk.Services.StrategyCopilot;
using CompeteDesk.Services.Notifications;

var builder = WebApplication.CreateBuilder(args);

var isDev = builder.Environment.IsDevelopment();

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// Needed for audit trail (CreatedBy/UpdatedBy) in ApplicationDbContext.
builder.Services.AddHttpContextAccessor();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Basic in-memory caching (used for frequent dropdown data like Workspaces/Strategies)
builder.Services.AddMemoryCache();

// ------------------------------------------------------------
// Website Analysis + OpenAI
// ------------------------------------------------------------
builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection("OpenAI"));

// ------------------------------------------------------------
// Gemini (Topbar AI Search)
// ------------------------------------------------------------
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection("Gemini"));
builder.Services.AddHttpClient<GeminiClient>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(40);
});

// HttpClient for site fetches (analysis).
builder.Services.AddHttpClient("site-analyzer", c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("CompeteDeskSiteAnalyzer/1.0");
});

// HttpClient for OpenAI.
builder.Services.AddHttpClient<OpenAiChatClient>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(40);
});

builder.Services.AddScoped<WebsiteAnalysisService>();
builder.Services.AddScoped<BusinessAnalysisService>();
builder.Services.AddScoped<WarRoomAiService>();
builder.Services.AddScoped<HabitsAiService>();
builder.Services.AddScoped<StrategyCopilotAiService>();
builder.Services.AddScoped<DecisionTraceService>();
builder.Services.AddScoped<AiContextPackBuilder>();
builder.Services.AddScoped<StrategyAiAssistService>();

// Strategic upgrades
builder.Services.AddScoped<CompeteDesk.Services.Gamification.GamificationService>();
builder.Services.AddScoped<CompeteDesk.Services.StudyPlanner.StudyPlannerService>();
builder.Services.AddScoped<CompeteDesk.Services.Recommendations.RecommendationsService>();
builder.Services.AddScoped<CompeteDesk.Services.Exports.ExportReportService>();

// Email/SMS providers
builder.Services.Configure<SendGridOptions>(builder.Configuration.GetSection("SendGrid"));
builder.Services.AddTransient<IEmailSender, SendGridEmailSender>();

builder.Services.Configure<TwilioOptions>(builder.Configuration.GetSection("Twilio"));
builder.Services.AddTransient<TwilioSmsSender>();

// Identity + External Login (Google)
builder.Services
    .AddDefaultIdentity<IdentityUser>(options =>
    {
        // For consumer apps, requiring confirmed account often blocks external logins
        // unless you implement an email confirmation flow. Keep it simple for now.
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Authorization policies (granular permissions)
builder.Services.AddAuthorization(options =>
{
    // CanEdit: allow creating/updating/deleting core data
    options.AddPolicy("CanEdit", policy =>
        policy.RequireRole(IdentitySeeder.AdminRoleName, IdentitySeeder.EditorRoleName));

    // Read-only users can still view app pages (authenticated) but can't POST/edit
    options.AddPolicy("ReadOnly", policy =>
        policy.RequireRole(IdentitySeeder.ReadOnlyRoleName));
});

// Cookie settings for external auth (fixes "Correlation failed" on some browsers)
builder.Services.ConfigureApplicationCookie(options =>
{
    // In Development we often run on http://localhost; Secure cookies would not be sent.
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});

builder.Services.ConfigureExternalCookie(options =>
{
    // External auth cookies: SameSite=None is required for cross-site OAuth in production,
    // but browsers require Secure for SameSite=None. For local dev over http, use Lax.
    options.Cookie.SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None;
    options.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});

// If CookiePolicy forces SameSite=Lax, it can break the OAuth roundtrip and cause
// "AuthenticationFailureException: Correlation failed." Ensure None cookies remain None.
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Unspecified;

    // Only force Secure when the cookie is SameSite=None AND we're on https.
    // (In local dev on http, forcing Secure breaks login persistence.)
    options.OnAppendCookie = ctx =>
    {
        if (!isDev && ctx.CookieOptions.SameSite == SameSiteMode.None)
        {
            ctx.CookieOptions.Secure = true;
        }
    };

    options.OnDeleteCookie = ctx =>
    {
        if (!isDev && ctx.CookieOptions.SameSite == SameSiteMode.None)
        {
            ctx.CookieOptions.Secure = true;
        }
    };
});


// External Login (Google) - only enable if credentials exist.
// Prevents runtime crash: ArgumentException "ClientId cannot be empty".
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    // IMPORTANT: Explicitly keep Identity defaults so the external sign-in cookie works reliably.
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        // CallbackPath defaults to /signin-google (keep default)

        // Make the correlation cookie compatible with modern SameSite rules.
        // Without this, Chrome/Safari can drop the correlation cookie and the callback
        // will fail with "Correlation failed".
        options.CorrelationCookie.SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None;
        options.CorrelationCookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    });
}

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Ensure Workspace CRUD works even if you already have an existing app.db.
// (Creates the Workspaces table if missing.)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DbBootstrapper.EnsureCoreTablesAsync(app.Services);

    // Ensure the Admin role exists and assign Admin to the configured seed email.
    // (Important for Google external login scenarios where we don't pre-create a password-based user.)
    await IdentitySeeder.EnsureAdminAsync(scope.ServiceProvider);

}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// In Development, Google external login callbacks can fail with HTTP 400 if the flow
// is initiated on http:// but redirected mid-flight to https:// (correlation/state mismatch).
// Keep dev stable by avoiding forced scheme changes.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();

// Must be before UseAuthentication so external auth cookies keep SameSite=None.
app.UseCookiePolicy();

// IMPORTANT: Auth must run before Authorization
app.UseAuthentication();

// Ensure every signed-in user has a baseline role (Editor by default).
// Also supports config-driven Admin promotion for the seed email.
app.Use(async (context, next) =>
{
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        using var scope = context.RequestServices.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var user = await userManager.GetUserAsync(context.User);
        if (user is not null)
        {
            // Promote configured seed email to Admin when it first signs in.
            var seedEmail = config["AdminSeed:Email"];
            if (!string.IsNullOrWhiteSpace(seedEmail)
                && string.Equals(user.Email, seedEmail, StringComparison.OrdinalIgnoreCase))
            {
                var roles = await userManager.GetRolesAsync(user);
                if (!roles.Contains(IdentitySeeder.AdminRoleName, StringComparer.OrdinalIgnoreCase))
                {
                    await userManager.AddToRoleAsync(user, IdentitySeeder.AdminRoleName);
                }
            }

            await IdentitySeeder.EnsureUserHasDefaultRoleAsync(scope.ServiceProvider, user);
        }
    }

    await next();
});

app.UseAuthorization();

// ------------------------------------------------------------
// Role-based onboarding gate
// If a signed-in user has not completed onboarding (UserProfiles record),
// redirect them to /Onboarding.
// ------------------------------------------------------------
app.Use(async (context, next) =>
{
    if (context.User?.Identity?.IsAuthenticated == true
        && (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)))
    {
        var path = context.Request.Path;

        // Allow Identity UI, static files, API endpoints, and the onboarding page itself.
        var skip = path.StartsWithSegments("/Onboarding", StringComparison.OrdinalIgnoreCase)
                   || path.StartsWithSegments("/Identity", StringComparison.OrdinalIgnoreCase)
                   || path.StartsWithSegments("/css", StringComparison.OrdinalIgnoreCase)
                   || path.StartsWithSegments("/js", StringComparison.OrdinalIgnoreCase)
                   || path.StartsWithSegments("/lib", StringComparison.OrdinalIgnoreCase)
                   || path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                   || path.StartsWithSegments("/favicon", StringComparison.OrdinalIgnoreCase);

        if (!skip)
        {
            var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                using var scope = context.RequestServices.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var hasProfile = await db.UserProfiles.AnyAsync(x => x.UserId == userId);
                if (!hasProfile)
                {
                    context.Response.Redirect("/Onboarding");
                    return;
                }
            }
        }
    }

    await next();
});

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();
