using Microsoft.AspNetCore.Authorization;
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
    [Authorize(Roles = "Admin")]
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
    public async Task<IActionResult> Register([Bind("Email,Password,Name,Phone,Birthday,Gender,CityId,Photo")] Member member)
    {
        if (ModelState.IsValid)
        {
            // Set defaults
            member.IsEnabled = true;
            member.IsMatchEnabled = true; // Enable match by default
            if(string.IsNullOrEmpty(member.Photo)) member.Photo = "/images/default-user.png";

            _context.Add(member);
            await _context.SaveChangesAsync();
            
            // --- AI Auto-Match & Welcome Logic (Phase 16 Fix) ---
            try 
            {
                 var aiEmail = "ai@petpet.com";
                 
                 // 1. Create Mutual Match with AI
                 _context.MatchInteractions.Add(new MatchInteraction { SourceMemberId = member.Email, TargetMemberId = aiEmail, Action = MatchAction.Like, CreatedAt = DateTime.UtcNow });
                 _context.MatchInteractions.Add(new MatchInteraction { SourceMemberId = aiEmail, TargetMemberId = member.Email, Action = MatchAction.Like, CreatedAt = DateTime.UtcNow });
                 
                 // 2. Initial Message from AI
                 _context.Messages.Add(new Message 
                 { 
                     SenderEmail = aiEmail, 
                     ReceiverEmail = member.Email, 
                     Content = "嗨！我是 PetPet 專屬的 AI 助手 🤖\n歡迎加入我們！\n有任何問題或是想聊聊毛小孩，隨時都可以找我喔！", 
                     SentAt = DateTime.UtcNow, 
                     IsRead = false 
                 });

                 await _context.SaveChangesAsync();
            }
            catch(Exception ex)
            {
                // Log but don't fail registration
                Console.WriteLine($"AI Match Error: {ex.Message}");
            }
            // ----------------------------------------------------

            // Auto Login
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, member.Name),
                new Claim(ClaimTypes.Email, member.Email),
                new Claim(ClaimTypes.NameIdentifier, member.Email) 
            };
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Index", "Home");
        }
        return View(member);
    }
}
