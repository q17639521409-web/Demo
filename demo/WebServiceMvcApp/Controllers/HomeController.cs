using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WebServiceMvcApp.Models;

namespace WebServiceMvcApp.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet("Home/Status")]
    public IActionResult Status()
    {
        var result = new
        {
            success = true,
            message = "Web service is running",
            timestamp = DateTime.UtcNow,
            data = new
            {
                machine = Environment.MachineName,
                framework = ".NET 9",
                mode = "MVC sample"
            }
        };

        return Json(result);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
