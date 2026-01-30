using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using CompeteDesk.Models;

namespace CompeteDesk.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        // If a user is already authenticated, send them straight to the app dashboard.
        // Identity's default UI commonly redirects to "/" after login when no returnUrl is provided.
        // This makes the post-login experience land on the dashboard automatically.
        if (User?.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
