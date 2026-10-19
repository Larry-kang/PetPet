using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;

namespace PetPet.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly PetPetDbContext _context;

    public AdminController(PetPetDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.UserCount = await _context.Members.CountAsync();
        ViewBag.PostCount = await _context.Posts.CountAsync();
        ViewBag.ReportCount = await _context.Reports.CountAsync();
        ViewBag.NewsCount = await _context.News.CountAsync();

        return View();
    }

    public async Task<IActionResult> ManageUsers(int page = 1, int pageSize = 20)
    {
        if (page < 1) page = 1;
        var query = _context.Members.AsNoTracking().OrderBy(m => m.Email);
        var total = await query.CountAsync();
        var users = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        ViewBag.TotalUsers = total;

        return View(users);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetUserEnabled(string email, bool enabled, string reason, Guid version)
    {
        var currentAdminEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.Equals(currentAdminEmail, email, StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status409Conflict, "無法停權目前登入之管理員帳號。");
        }

        var target = await _context.Members.FirstOrDefaultAsync(m => m.Email == email);
        if (target == null)
        {
            return NotFound("找不到指定會員。");
        }

        // Optimistic concurrency check
        if (target.Version != version)
        {
            return StatusCode(StatusCodes.Status409Conflict, "會員狀態已被其他管理員變更，請重新整理後再試。");
        }

        // Prevent disabling the last active admin
        if (!enabled && target.IsAdmin)
        {
            int activeAdmins = await _context.Members.CountAsync(m => m.IsAdmin && m.IsEnabled);
            if (activeAdmins <= 1)
            {
                return StatusCode(StatusCodes.Status409Conflict, "無法停權系統中最後一名啟用中的管理員。");
            }
        }

        bool oldEnabled = target.IsEnabled;
        target.IsEnabled = enabled;
        target.SecurityStamp = Guid.NewGuid().ToString("N"); // Revoke all active sessions
        target.Version = Guid.NewGuid();

        var audit = new AdminAudit
        {
            ActorEmail = currentAdminEmail ?? "system",
            TargetId = target.Email,
            Action = enabled ? "EnableMember" : "DisableMember",
            Reason = string.IsNullOrWhiteSpace(reason) ? "管理員手動調整狀態" : reason.Trim(),
            BeforeJson = $"{{\"IsEnabled\":{oldEnabled.ToString().ToLower()}}}",
            AfterJson = $"{{\"IsEnabled\":{enabled.ToString().ToLower()}}}",
            CreatedAt = DateTime.UtcNow,
            TraceId = HttpContext.TraceIdentifier
        };
        _context.AdminAudits.Add(audit);

        await _context.SaveChangesAsync();
        TempData["Message"] = $"會員 {target.Name} ({target.Email}) 狀態已變更為 {(enabled ? "啟用" : "停權")}。";

        return RedirectToAction(nameof(ManageUsers));
    }

    // Retain legacy ToggleUser for backwards compatibility, delegating to SetUserEnabled logic
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUser(string email)
    {
        var target = await _context.Members.FirstOrDefaultAsync(m => m.Email == email);
        if (target == null) return NotFound();
        return await SetUserEnabled(email, !target.IsEnabled, "快速切換狀態", target.Version);
    }

    public async Task<IActionResult> ManageReports(string status = "Pending", int page = 1)
    {
        if (page < 1) page = 1;
        const int pageSize = 20;

        string normalizedStatus = status.ToLowerInvariant() switch
        {
            "resolved" => "Resolved",
            "dismissed" => "Dismissed",
            _ => "Pending"
        };

        var baseQuery = _context.Reports.AsNoTracking();

        int pendingCount = await baseQuery.CountAsync(r => r.Status == "Pending");
        int resolvedCount = await baseQuery.CountAsync(r => r.Status == "Resolved");
        int dismissedCount = await baseQuery.CountAsync(r => r.Status == "Dismissed");

        var query = baseQuery
            .Include(r => r.Post)
            .Include(r => r.Reporter)
            .Include(r => r.HandledBy)
            .Where(r => r.Status == normalizedStatus);

        int totalItems = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        if (totalPages > 0 && page > totalPages) page = totalPages;

        var reports = await query
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new PetPet.Application.Moderation.ReportItemDto
            {
                Id = r.Id,
                PostId = r.PostId,
                PostTitle = r.Post != null ? r.Post.Title : "(已刪除貼文)",
                PostAuthorName = r.Post != null && r.Post.Author != null ? r.Post.Author.Name : "作者",
                PostIsEnabled = r.Post != null && r.Post.IsEnabled,
                PostIsDeleted = r.Post != null && r.Post.IsDeleted,
                ReporterEmail = r.ReporterEmail,
                ReporterName = r.Reporter.Name,
                Category = r.Category,
                Reason = r.Reason,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                HandledAt = r.HandledAt,
                HandledByName = r.HandledBy != null ? r.HandledBy.Name : null,
                HandlingNote = r.HandlingNote,
                Version = r.Version
            })
            .ToListAsync();

        var viewModel = new PetPet.Application.Moderation.AdminReportsViewModel
        {
            Status = normalizedStatus,
            PendingCount = pendingCount,
            ResolvedCount = resolvedCount,
            DismissedCount = dismissedCount,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalItems = totalItems,
            Reports = reports
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveReport(int id, Guid version, bool hidePost = false, string? note = null)
    {
        var currentAdminEmail = User.FindFirstValue(ClaimTypes.Email);

        var report = await _context.Reports.Include(r => r.Post).FirstOrDefaultAsync(r => r.Id == id);
        if (report == null) return NotFound("找不到指定檢舉案件。");

        if (report.Status != "Pending")
        {
            return StatusCode(StatusCodes.Status409Conflict, "該檢舉案件已被結案，無法重複處理。");
        }

        if (report.Version != version)
        {
            return StatusCode(StatusCodes.Status409Conflict, "案件資料已被其他管理員變更，請重新整理後再試。");
        }

        report.Status = "Resolved";
        report.HandledAt = DateTime.UtcNow;
        report.HandledByEmail = currentAdminEmail;
        report.HandlingNote = string.IsNullOrWhiteSpace(note) ? "處置完成" : note.Trim();
        report.Version = Guid.NewGuid();

        if (hidePost && report.Post != null)
        {
            report.Post.IsEnabled = false;
            report.Post.Version = Guid.NewGuid();
        }

        var audit = new AdminAudit
        {
            ActorEmail = currentAdminEmail ?? "system",
            TargetId = report.Id.ToString(),
            Action = hidePost ? "ResolveReportAndHidePost" : "ResolveReport",
            Reason = report.HandlingNote,
            CreatedAt = DateTime.UtcNow,
            TraceId = HttpContext.TraceIdentifier
        };
        _context.AdminAudits.Add(audit);

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"檢舉案件 #{report.Id} 已處置完畢。{(hidePost ? "（貼文已被隱藏）" : "")}";

        return RedirectToAction(nameof(ManageReports), new { status = "Pending" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DismissReport(int id, Guid version, string note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            TempData["ErrorMessage"] = "駁回檢舉必須附上理由。";
            return RedirectToAction(nameof(ManageReports), new { status = "Pending" });
        }

        var currentAdminEmail = User.FindFirstValue(ClaimTypes.Email);

        var report = await _context.Reports.FirstOrDefaultAsync(r => r.Id == id);
        if (report == null) return NotFound("找不到指定檢舉案件。");

        if (report.Status != "Pending")
        {
            return StatusCode(StatusCodes.Status409Conflict, "該檢舉案件已被結案。");
        }

        if (report.Version != version)
        {
            return StatusCode(StatusCodes.Status409Conflict, "案件資料已被其他管理員變更，請重新整理後再試。");
        }

        report.Status = "Dismissed";
        report.HandledAt = DateTime.UtcNow;
        report.HandledByEmail = currentAdminEmail;
        report.HandlingNote = note.Trim();
        report.Version = Guid.NewGuid();

        var audit = new AdminAudit
        {
            ActorEmail = currentAdminEmail ?? "system",
            TargetId = report.Id.ToString(),
            Action = "DismissReport",
            Reason = report.HandlingNote,
            CreatedAt = DateTime.UtcNow,
            TraceId = HttpContext.TraceIdentifier
        };
        _context.AdminAudits.Add(audit);

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"已駁回檢舉案件 #{report.Id}。";

        return RedirectToAction(nameof(ManageReports), new { status = "Pending" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestorePost(int postId)
    {
        var currentAdminEmail = User.FindFirstValue(ClaimTypes.Email);

        var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == postId);
        if (post == null) return NotFound("找不到指定貼文。");

        if (post.IsDeleted)
        {
            TempData["ErrorMessage"] = "貼文已被作者刪除，無法復原公開。";
            return RedirectToAction(nameof(ManageReports));
        }

        post.IsEnabled = true;
        post.Version = Guid.NewGuid();

        var audit = new AdminAudit
        {
            ActorEmail = currentAdminEmail ?? "system",
            TargetId = post.Id.ToString(),
            Action = "RestorePost",
            Reason = "管理員復原貼文公開顯示",
            CreatedAt = DateTime.UtcNow,
            TraceId = HttpContext.TraceIdentifier
        };
        _context.AdminAudits.Add(audit);

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"貼文 #{post.Id} 已成功復原公開顯示！";

        return RedirectToAction(nameof(ManageReports));
    }
}
