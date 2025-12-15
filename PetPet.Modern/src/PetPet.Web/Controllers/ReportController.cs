using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;
using System.Security.Claims;

namespace PetPet.Web.Controllers;

[Authorize]
public class ReportController : Controller
{
    private readonly PetPetDbContext _context;

    public ReportController(PetPetDbContext context)
    {
        _context = context;
    }

    // GET: Report/Create?postId=5
    public async Task<IActionResult> Create(int postId)
    {
        var post = await _context.Posts
            .Include(p => p.Author)
            .FirstOrDefaultAsync(p => p.Id == postId);

        if (post == null) return NotFound();

        ViewData["PostTitle"] = post.Title;
        ViewData["PostAuthor"] = post.Author.Name;

        return View(new Report { PostId = postId });
    }

    // POST: Report/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("PostId,Reason")] Report report)
    {
        if (ModelState.IsValid)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

            report.ReporterEmail = userEmail;
            report.CreatedAt = DateTime.UtcNow;
            report.Status = "Pending";

            _context.Add(report);
            await _context.SaveChangesAsync();
            
            // Redirect to Post Feed with success message (can use TempData)
            return RedirectToAction("Index", "Post");
        }
        return View(report);
    }
}
