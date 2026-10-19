using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PetPet.Application.Members;
using System.Security.Claims;

namespace PetPet.Web.Controllers;

public class MemberController : Controller
{
    private readonly MemberAccountService _accounts;

    public MemberController(MemberAccountService accounts)
    {
        _accounts = accounts;
    }

    // GET: Member/Login
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    // POST: Member/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login-limit")]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
    {
        var member = await _accounts.AuthenticateAsync(email, password);

        if (member == null)
        {
            ViewData["Error"] = "帳號或密碼錯誤，或此帳號已被停權";
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, member.Name),
            new Claim(ClaimTypes.Email, member.Email),
            new Claim(ClaimTypes.NameIdentifier, member.Email),
            new Claim("PublicId", member.PublicId.ToString()),
            new Claim("SecurityStamp", member.SecurityStamp),
            new Claim("MemberId", member.Email)
        };

        if (member.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        claimsIdentity.AddClaim(new Claim("IssuedUtc", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            IssuedUtc = DateTimeOffset.UtcNow
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Post");
    }

    // POST: Member/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    // GET: Member/Logout - Refuses to sign out on GET per PP-SEC-02
    [HttpGet]
    public IActionResult Logout(string? dummy = null)
    {
        return RedirectToAction("Index", "Home");
    }

    // GET: Member
    public IActionResult Index()
    {
        if (User.IsInRole("Admin"))
        {
            return RedirectToAction("ManageUsers", "Admin");
        }
        return RedirectToAction("Index", "Home");
    }

    // GET: Member/Register
    public IActionResult Register()
    {
        return View();
    }

    // POST: Member/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("register-limit")]
    public async Task<IActionResult> Register(RegisterMember member)
    {
        if (ModelState.IsValid)
        {
            if (await _accounts.RegisterAsync(member))
                return RedirectToAction(nameof(Login));
            ModelState.AddModelError(string.Empty, "此 Email 已註冊");
        }
        return View(member);
    }
}
