using Xunit;
using CompeteDesk.Application.Services;

public class WorkspaceServiceTests
{
    [Fact]
    public void CreateWorkspace_ShouldReturnValidWorkspace()
    {
        var service = new WorkspaceService();
        var result = service.Create("Test Workspace");

        Assert.NotNull(result);
        Assert.Equal("Test Workspace", result.Name);
    }
}