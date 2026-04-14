using Xunit;
using Microsoft.AspNetCore.Mvc;
using CompeteDesk.Web.Controllers;

public class DashboardControllerTests
{
    [Fact]
    public void Index_ReturnsView()
    {
        var controller = new DashboardController();
        var result = controller.Index();

        Assert.IsType<ViewResult>(result);
    }
}