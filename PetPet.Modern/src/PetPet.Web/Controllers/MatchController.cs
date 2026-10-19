using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetPet.Infrastructure.Services;

namespace PetPet.Web.Controllers;

[Authorize]
public class MatchController : Controller
{
    private readonly IMatchService _matchService;

    public MatchController(IMatchService matchService)
    {
        _matchService = matchService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(userEmail))
        {
            return RedirectToAction("Login", "Member");
        }

        var model = await _matchService.GetPlaymateRecommendationsAsync(userEmail);
        return View(model);
    }
}
