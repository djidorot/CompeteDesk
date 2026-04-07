using Microsoft.AspNetCore.Http;

namespace CompeteDesk.Infrastructure;

public static class AppShellPolicy
{
    public static bool ShouldUseSidebar(bool requestedSidebar, bool isAuthenticated)
        => requestedSidebar && isAuthenticated;

    public static bool ShouldShowLandingCta(bool isAuthenticated)
        => !isAuthenticated;

    public static bool ShouldSkipOnboardingGate(PathString path)
    {
        if (!path.HasValue || path == "/")
        {
            return true;
        }

        return path.StartsWithSegments("/Home")
               || path.StartsWithSegments("/Privacy")
               || path.StartsWithSegments("/Onboarding")
               || path.StartsWithSegments("/Identity")
               || path.StartsWithSegments("/css")
               || path.StartsWithSegments("/js")
               || path.StartsWithSegments("/lib")
               || path.StartsWithSegments("/images")
               || path.StartsWithSegments("/api")
               || path.StartsWithSegments("/favicon")
               || path.StartsWithSegments("/updating");
    }
}
