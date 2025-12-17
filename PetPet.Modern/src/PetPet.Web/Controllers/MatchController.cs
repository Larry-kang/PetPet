using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetPet.Infrastructure.Services;
using PetPet.Domain.Entities;
using System.Security.Claims;

using PetPet.Application.Services;

namespace PetPet.Web.Controllers
{
    [Authorize]
    public class MatchController : Controller
    {
        private readonly IMatchService _matchService;
        private readonly ZiweiService _ziweiService;

        public MatchController(IMatchService matchService, ZiweiService ziweiService)
        {
            _matchService = matchService;
            _ziweiService = ziweiService;
        }

        public async Task<IActionResult> Index()
        {
            // Initial Load - We will load cards via AJAX mainly, but first one can be server rendered or JS
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetCandidates()
        {
            var userEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

            // Calculate My Star
            // In real app, we get Current User Birthday. Currently Claims don't include Birthday.
            // We'll fake it or fetch user. For performance, let's just fetch user profile or pass in a simple mock.
            // PROPER WAY: Retrieve user from Service or DB.
            // Since we don't have GetProfile in MatchService, let's assume random star for 'Me' or use simple hash of email for consistency in demo.
            
            var matches = await _matchService.CleanFindMatchesAsync(userEmail);
            
            // Enrich with Ziwei Data
            var enrichedMatches = matches.Select(m => 
            {
                // Fake Birthday for Demo Candidate based on Email Hash
                int hash = Math.Abs(m.TargetMember.Email.GetHashCode());
                var simulatedBirthday = new DateTime(1995, (hash % 12) + 1, (hash % 28) + 1); 
                var star = _ziweiService.CalculateStar(simulatedBirthday);
                var analysis = _ziweiService.GetAnalysis(star);
                
                // My Star
                var mySimulatedBirthday = new DateTime(1990, (userEmail.Length % 12) + 1, 15);
                var myStar = _ziweiService.CalculateStar(mySimulatedBirthday);
                
                var score = _ziweiService.CalculateMatchScore(myStar, star);

                return new 
                {
                    // No Id for Member, use Email as Key
                    m.TargetMember.Email,
                    m.TargetMember.Name,
                    m.TargetMember.Photo,
                    Age = (DateTime.Now.Year - m.TargetMember.Birthday.Year),
                    m.TargetMember.CityId, 
                    Bio = "熱愛毛小孩的...", 
                    Tags = new List<string> { "愛狗", "戶外" }, 
                    Pet = m.PrimaryPet,
                    // Ziwei Props
                    ZiweiStarObj = star,
                    ZiweiName = analysis.Name,
                    ZiweiDesc = analysis.Personality,
                    MatchScore = score,
                    m.MatchReasons
                };
            });

            return Json(enrichedMatches);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Swipe([FromForm] string targetEmail, [FromForm] MatchAction action)
        {
            var userEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

            bool isMatch = await _matchService.RecordSwipeAsync(userEmail, targetEmail, action);
            
            return Json(new { success = true, isMatch = isMatch });
        }
    }
}
