using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;
using System.Security.Claims;

namespace PetPet.Web.Controllers;

public class MemberController : Controller
{
    private readonly PetPetDbContext _context;

    public MemberController(PetPetDbContext context)
    {
        _context = context;
    }

    // GET: Member/Login
    public IActionResult Login()
    {
        return View();
    }

    // POST: Member/Login
    [HttpPost]
    public async Task<IActionResult> Login(string email, string password)
    {
        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.Email == email && m.Password == password);

        if (member == null)
        {
            ViewData["Error"] = "帳號或密碼錯誤";
            return View();
        }

        if (!member.IsEnabled)
        {
            ViewData["Error"] = "此帳號已被停權";
            return View();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, member.Name),
            new Claim(ClaimTypes.Email, member.Email),
            new Claim(ClaimTypes.NameIdentifier, member.Email), // Critical for SignalR
            new Claim("MemberId", member.Email) // Legacy support
        };

        if (member.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties();

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        return RedirectToAction("Index", "Home");
    }

    // GET: Member/Logout
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    // GET: Member
    public async Task<IActionResult> Index()
    {
        return View(await _context.Members.ToListAsync());
    }

    // GET: Member/Register
    public IActionResult Register()
    {
        return View();
    }

    // POST: Member/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register([Bind("Email,Password,Name,Phone")] Member member)
    {
        if (ModelState.IsValid)
        {
            // Set defaults for required fields not in form
            member.Birthday = DateTime.Now; // Temporary default
            member.CityId = 1; // Temporary default
            member.Gender = true; // Temporary default
            member.IsEnabled = true;
            member.IsMatchEnabled = false;
            member.Photo = "default.jpg";

            _context.Add(member);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(member);
    }
}
