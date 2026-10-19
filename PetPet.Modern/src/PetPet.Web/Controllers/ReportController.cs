using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PetPet.Application.Moderation;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;

namespace PetPet.Web.Controllers;

[Authorize]
public class ReportController : Controller
{
    private readonly PetPetDbContext _context;

    private static readonly HashSet<string> ValidCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Spam",
        "Harassment",
        "Inappropriate",
        "Other"
    };

    public ReportController(PetPetDbContext context)
    {
        _context = context;
    }

    // GET: Report/Create?postId=5
    [HttpGet]
    public async Task<IActionResult> Create(int postId)
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

        var post = await _context.Posts.AsNoTracking()
            .Include(p => p.Author)
            .FirstOrDefaultAsync(p => p.Id == postId && p.IsEnabled && !p.IsDeleted);

        if (post == null)
        {
            return NotFound("找不到指定貼文或該貼文已被刪除。");
        }

        if (string.Equals(post.AuthorEmail, userEmail, StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "無法檢舉自己的貼文。";
            return BadRequest("無法檢舉自己的貼文。");
        }

        ViewData["PostTitle"] = post.Title;
        ViewData["PostAuthor"] = post.Author.Name;

        return View(new CreateReportDto { PostId = postId });
    }

    // POST: Report/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("report-limit")]
    public async Task<IActionResult> Create(CreateReportDto model)
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

        if (string.Equals(model.Category, "Other", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(model.Reason))
        {
            ModelState.AddModelError(nameof(model.Reason), "選擇「其他」類別時，請填寫補充說明。");
        }

        if (!ValidCategories.Contains(model.Category))
        {
            ModelState.AddModelError(nameof(model.Category), "請選擇有效的檢舉類別。");
        }

        var post = await _context.Posts.AsNoTracking()
            .Include(p => p.Author)
            .FirstOrDefaultAsync(p => p.Id == model.PostId && p.IsEnabled && !p.IsDeleted);

        if (post == null)
        {
            return NotFound("找不到指定貼文。");
        }

        if (string.Equals(post.AuthorEmail, userEmail, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("無法檢舉自己的貼文。");
        }

        if (!ModelState.IsValid)
        {
            ViewData["PostTitle"] = post.Title;
            ViewData["PostAuthor"] = post.Author.Name;
            return View(model);
        }

        // Check for duplicate pending report
        var existingPending = await _context.Reports.AsNoTracking()
            .FirstOrDefaultAsync(r => r.PostId == model.PostId && r.ReporterEmail == userEmail && r.Status == "Pending");

        if (existingPending != null)
        {
            TempData["ErrorMessage"] = "您先前已對此貼文送出檢舉，管理員正在審查中，請勿重複提交。";
            return RedirectToAction("Details", "Post", new { id = model.PostId });
        }

        var report = new Report
        {
            PostId = model.PostId,
            ReporterEmail = userEmail,
            Category = model.Category,
            Reason = model.Reason?.Trim() ?? string.Empty,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            Version = Guid.NewGuid()
        };

        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "檢舉已成功送出，感謝您協助維護社群品質！";
        return RedirectToAction("Details", "Post", new { id = model.PostId });
    }
}
