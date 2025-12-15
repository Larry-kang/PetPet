using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PetPet.Web.Models;

namespace PetPet.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        // 舊版還原: 如果已登入，直接跳轉到社群動態牆 (Post/Index)
        // Legacy Feature: Redirect logged-in users to Post Feed
        if (User.Identity!.IsAuthenticated)
        {
            return RedirectToAction("Index", "Post");
        }
        
        // 訪客則顯示現代化首頁
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
