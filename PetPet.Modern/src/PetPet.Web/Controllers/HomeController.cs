using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetPet.Infrastructure.Data;
using PetPet.Web.Models;

namespace PetPet.Web.Controllers;

public class HomeController : Controller
{
    private readonly PetPetDbContext _context;
    private readonly IWebHostEnvironment _env;

    public HomeController(PetPetDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    public async Task<IActionResult> Index()
    {
        // 舊版還原: 如果已登入，直接跳轉到社群動態牆 (Post/Index)
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Post");
        }

        bool isDemo = _env.IsEnvironment("Demo");
        ViewBag.IsDemo = isDemo;

        if (isDemo)
        {
            try
            {
                ViewBag.MemberCount = await _context.Members.CountAsync();
                ViewBag.PetCount = await _context.Pets.CountAsync();
                ViewBag.PostCount = await _context.Posts.CountAsync();
                ViewBag.CommentCount = await _context.Comments.CountAsync();
            }
            catch
            {
                ViewBag.MemberCount = 0;
                ViewBag.PetCount = 0;
                ViewBag.PostCount = 0;
                ViewBag.CommentCount = 0;
            }
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
