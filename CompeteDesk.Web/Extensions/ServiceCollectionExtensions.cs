using CompeteDesk.Data;
using CompeteDesk.Services.Ai;
using CompeteDesk.Services.BusinessAnalysis;
using CompeteDesk.Services.Gemini;
using CompeteDesk.Services.Habits;
using CompeteDesk.Services.Notifications;
using CompeteDesk.Services.OpenAI;
using CompeteDesk.Services.StrategyCopilot;
using CompeteDesk.Services.WarRoom;
using CompeteDesk.Services.WebsiteAnalysis;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CompeteDesk.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCompeteDeskApplication(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var isDev = environment.IsDevelopment();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        var normalizedConnectionString = SqliteConnectionStringHelper.NormalizeForAppData(environment, connectionString);

        services.AddDbContext<ApplicationDbContext>(options =>
            options
                .UseSqlite(normalizedConnectionString)
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        services.AddHttpContextAccessor();
        services.AddDatabaseDeveloperPageExceptionFilter();
        services.AddMemoryCache();
        services.AddControllersWithViews();
        services.AddScoped<CompeteDesk.Services.ActiveWorkspaceService>();

        ConfigureAiServices(services, configuration);
        ConfigureMessaging(services, configuration);
        ConfigureIdentity(services, configuration, isDev);

        return services;
    }

    private static void ConfigureAiServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAiOptions>(configuration.GetSection("OpenAI"));
        services.Configure<GeminiOptions>(configuration.GetSection("Gemini"));

        services.AddHttpClient<GeminiClient>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(40);
        });

        services.AddHttpClient("site-analyzer", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("CompeteDeskSiteAnalyzer/1.0");
        });

        services.AddHttpClient<OpenAiChatClient>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(120);
        });

        services.AddScoped<WebsiteAnalysisService>();
        services.AddScoped<BusinessAnalysisService>();
        services.AddScoped<WarRoomAiService>();
        services.AddScoped<HabitsAiService>();
        services.AddScoped<StrategyCopilotAiService>();
        services.AddScoped<DecisionTraceService>();
        services.AddScoped<AiContextPackBuilder>();
        services.AddScoped<StrategyAiAssistService>();
        services.AddScoped<CompeteDesk.Services.Gamification.GamificationService>();
        services.AddScoped<CompeteDesk.Services.StudyPlanner.StudyPlannerService>();
        services.AddScoped<CompeteDesk.Services.Recommendations.RecommendationsService>();
        services.AddScoped<CompeteDesk.Services.Exports.ExportReportService>();
    }

    private static void ConfigureMessaging(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SendGridOptions>(configuration.GetSection("SendGrid"));
        var sendGridConfigured = !string.IsNullOrWhiteSpace(configuration["SendGrid:ApiKey"])
            && !string.IsNullOrWhiteSpace(configuration["SendGrid:FromEmail"]);

        services.AddTransient<IEmailSender>(sp =>
            sendGridConfigured
                ? new SendGridEmailSender(
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SendGridOptions>>(),
                    sp.GetRequiredService<ILogger<SendGridEmailSender>>())
                : new NullEmailSender(sp.GetRequiredService<ILogger<NullEmailSender>>()));

        services.Configure<TwilioOptions>(configuration.GetSection("Twilio"));
        var twilioConfigured = !string.IsNullOrWhiteSpace(configuration["Twilio:AccountSid"])
            && !string.IsNullOrWhiteSpace(configuration["Twilio:AuthToken"])
            && !string.IsNullOrWhiteSpace(configuration["Twilio:FromPhoneNumber"]);

        if (twilioConfigured)
        {
            services.AddTransient<TwilioSmsSender>();
        }
    }

    private static void ConfigureIdentity(IServiceCollection services, IConfiguration configuration, bool isDev)
    {
        services
            .AddDefaultIdentity<IdentityUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddAuthorization(options =>
        {
            options.AddPolicy("CanEdit", policy =>
                policy.RequireRole(
                    IdentitySeeder.AdminRoleName,
                    IdentitySeeder.EditorRoleName,
                    IdentitySeeder.UserRoleName));

            options.AddPolicy("ReadOnly", policy =>
                policy.RequireRole(IdentitySeeder.ReadOnlyRoleName));
        });

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.LoginPath = "/Identity/Account/Login";
            options.LogoutPath = "/Identity/Account/Logout";
            options.AccessDeniedPath = "/Identity/Account/AccessDenied";
        });

        services.ConfigureExternalCookie(options =>
        {
            options.Cookie.SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None;
            options.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });

        services.Configure<CookiePolicyOptions>(options =>
        {
            options.MinimumSameSitePolicy = SameSiteMode.Unspecified;
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

        var googleClientId = configuration["Authentication:Google:ClientId"];
        var googleClientSecret = configuration["Authentication:Google:ClientSecret"];

        if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
        {
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddGoogle(options =>
            {
                options.ClientId = googleClientId;
                options.ClientSecret = googleClientSecret;
                options.CorrelationCookie.SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None;
                options.CorrelationCookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            });
        }
    }
}
