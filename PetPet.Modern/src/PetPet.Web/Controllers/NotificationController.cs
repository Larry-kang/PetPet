using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;

namespace PetPet.Web.Controllers;

public class NotificationViewModel
{
    public List<Notification> Items { get; set; } = [];
    public int UnreadCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalItems { get; set; }
    public bool HasPrevious => CurrentPage > 1;
    public bool HasNext => CurrentPage < TotalPages;
}

[Authorize]
public class NotificationController : Controller
{
    private readonly PetPetDbContext _context;

    public NotificationController(PetPetDbContext context)
    {
        _context = context;
    }

    // GET: /Notification
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        if (page < 1) page = 1;
        const int pageSize = 20;

        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

        var query = _context.Notifications.AsNoTracking()
            .Include(n => n.Actor)
            .Where(n => n.RecipientEmail == userEmail);

        int totalItems = await query.CountAsync();
        int unreadCount = await query.CountAsync(n => n.ReadAt == null);

        int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        if (totalPages > 0 && page > totalPages) page = totalPages;

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var viewModel = new NotificationViewModel
        {
            Items = items,
            UnreadCount = unreadCount,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalItems = totalItems
        };

        return View(viewModel);
    }

    // POST: /Notification/MarkRead
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, string? returnUrl = null)
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

        var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.RecipientEmail == userEmail);
        if (notification != null && !notification.ReadAt.HasValue)
        {
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: /Notification/MarkAllRead
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(string? returnUrl = null)
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

        var unreadNotifications = await _context.Notifications
            .Where(n => n.RecipientEmail == userEmail && n.ReadAt == null)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var n in unreadNotifications)
        {
            n.ReadAt = now;
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "已將全部通知標示為已讀。";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }
}
