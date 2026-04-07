using CompeteDesk.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace CompeteDesk.Tests;

public class AppShellPolicyTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void ShouldUseSidebar_RequiresRequestedSidebarAndAuthenticatedUser(bool requested, bool isAuthenticated, bool expected)
    {
        var result = AppShellPolicy.ShouldUseSidebar(requested, isAuthenticated);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Home")]
    [InlineData("/Identity/Account/Login")]
    [InlineData("/css/site.css")]
    [InlineData("/updating")]
    public void ShouldSkipOnboardingGate_ReturnsTrueForPublicPaths(string path)
    {
        var result = AppShellPolicy.ShouldSkipOnboardingGate(new PathString(path));

        Assert.True(result);
    }

    [Theory]
    [InlineData("/Dashboard")]
    [InlineData("/Workspaces")]
    [InlineData("/Strategies/Create")]
    public void ShouldSkipOnboardingGate_ReturnsFalseForProtectedAppPaths(string path)
    {
        var result = AppShellPolicy.ShouldSkipOnboardingGate(new PathString(path));

        Assert.False(result);
    }
}
