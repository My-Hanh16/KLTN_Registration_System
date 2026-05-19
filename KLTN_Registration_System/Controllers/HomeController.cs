using KLTN_Registration_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace KLTN_Registration_System.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            // Chưa đăng nhập
            if (!User.Identity!.IsAuthenticated)
                return RedirectToAction("Login", "Account");

            // Điều hướng theo role
            if (User.IsInRole("Admin"))
                return RedirectToAction("Statistics", "Admin");

            if (User.IsInRole("Lecturer"))
                return RedirectToAction("Index", "Lecturer");

            if (User.IsInRole("Student"))
                return RedirectToAction("Home", "Student");

            // fallback
            return RedirectToAction("Login", "Account");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}