using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
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

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

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
        options.SignIn.RequireConfirmedAccount = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services
    .AddAuthentication()
    ;

// External Login (Google) - only enable if credentials exist.
// Prevents runtime crash: ArgumentException "ClientId cannot be empty".
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services
        .AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
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

app.UseHttpsRedirection();

app.UseRouting();

// IMPORTANT: Auth must run before Authorization
app.UseAuthentication();
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
