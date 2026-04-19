using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MentorMatch.Models;

namespace MentorMatch.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("Student")) return RedirectToAction("Dashboard", "Student");
            if (User.IsInRole("Supervisor")) return RedirectToAction("Dashboard", "Supervisor");
            if (User.IsInRole("Admin")) return RedirectToAction("Analytics", "Admin");
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
