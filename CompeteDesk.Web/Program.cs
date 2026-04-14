using CompeteDesk.Data;
using CompeteDesk.Extensions;
using CompeteDesk.Middleware;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

if (OperatingSystem.IsLinux() && !string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
{
    AppContext.SetSwitch("Microsoft.Extensions.FileProviders.UsePollingFileWatcher", true);
    AppContext.SetSwitch("Microsoft.Extensions.Hosting.IgnoreConfigFileExceptions", false);
    Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCompeteDeskApplication(builder.Configuration, builder.Environment);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var db = services.GetRequiredService<ApplicationDbContext>();

    var hasMigrations = (await db.Database.GetMigrationsAsync()).Any();

    if (hasMigrations)
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        logger.LogWarning("No EF Core migrations were found for {Context}. Skipping Database.MigrateAsync() and using bootstrap-based schema setup.", nameof(ApplicationDbContext));
    }

    await DbBootstrapper.EnsureCoreTablesAsync(db);
    await IdentitySeeder.EnsureAdminAsync(services);
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};

forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedHeadersOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseCookiePolicy();
app.UseAuthentication();
app.UseMiddleware<EnsureUserRoleMiddleware>();
app.UseAuthorization();
app.UseMiddleware<FeaturePermissionMiddleware>();
app.UseMiddleware<OnboardingGateMiddleware>();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();


app.Run();
