using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetPet.Infrastructure.Services;
using System.Security.Claims;

namespace PetPet.Web.Controllers
{
    [Authorize]
    public class MatchController : Controller
    {
        private readonly IMatchService _matchService;

        public MatchController(IMatchService matchService)
        {
            _matchService = matchService;
        }

        public async Task<IActionResult> Index()
        {
            var userEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value;

            if (string.IsNullOrEmpty(userEmail))
            {
                 return RedirectToAction("Login", "Member"); 
            }

            // Execute Algorithm
            var matches = await _matchService.CleanFindMatchesAsync(userEmail);
            
            return View(matches);
        }
    }
}
