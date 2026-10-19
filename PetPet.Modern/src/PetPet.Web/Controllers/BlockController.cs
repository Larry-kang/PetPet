using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;

namespace PetPet.Web.Controllers;

public class BlockedUserDto
{
    public int Id { get; set; }
    public string BlockedEmail { get; set; } = string.Empty;
    public string BlockedName { get; set; } = string.Empty;
    public Guid BlockedPublicId { get; set; }
    public string? BlockedPhoto { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Authorize]
public class BlockController : Controller
{
    private readonly PetPetDbContext _context;

    public BlockController(PetPetDbContext context)
    {
        _context = context;
    }

    // GET: /Block
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var currentEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(currentEmail)) return Unauthorized();

        var blocks = await _context.Blocks.AsNoTracking()
            .Include(b => b.Blocked)
            .Where(b => b.BlockerEmail == currentEmail)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BlockedUserDto
            {
                Id = b.Id,
                BlockedEmail = b.BlockedEmail,
                BlockedName = b.Blocked.Name,
                BlockedPublicId = b.Blocked.PublicId,
                BlockedPhoto = b.Blocked.Photo,
                CreatedAt = b.CreatedAt
            })
            .ToListAsync();

        return View(blocks);
    }

    // POST: /Block/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string? targetEmail, Guid? targetPublicId, string? returnUrl = null)
    {
        var currentEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(currentEmail)) return Unauthorized();

        Member? targetMember = null;
        if (!string.IsNullOrEmpty(targetEmail))
        {
            targetMember = await _context.Members.FirstOrDefaultAsync(m => m.Email == targetEmail);
        }
        else if (targetPublicId.HasValue)
        {
            targetMember = await _context.Members.FirstOrDefaultAsync(m => m.PublicId == targetPublicId.Value);
        }

        if (targetMember == null || !targetMember.IsEnabled)
        {
            TempData["ErrorMessage"] = "找不到指定的會員。";
            return NotFound("找不到指定的會員。");
        }

        if (string.Equals(targetMember.Email, currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "無法封鎖自己。";
            return BadRequest("無法封鎖自己。");
        }

        // Check if already blocked
        var existingBlock = await _context.Blocks.FirstOrDefaultAsync(b =>
            b.BlockerEmail == currentEmail && b.BlockedEmail == targetMember.Email);

        if (existingBlock == null)
        {
            _context.Blocks.Add(new Block
            {
                BlockerEmail = currentEmail,
                BlockedEmail = targetMember.Email,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Sever any existing friendship or pending requests in the same transaction
        string memberA = string.CompareOrdinal(currentEmail, targetMember.Email) < 0 ? currentEmail : targetMember.Email;
        string memberB = string.CompareOrdinal(currentEmail, targetMember.Email) < 0 ? targetMember.Email : currentEmail;

        var existingFriend = await _context.Friends.FirstOrDefaultAsync(f =>
            f.MemberAEmail == memberA && f.MemberBEmail == memberB);

        if (existingFriend != null)
        {
            _context.Friends.Remove(existingFriend);
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"已成功將 {targetMember.Name} 加入封鎖名單。";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: /Block/Remove
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int? id, string? targetEmail, string? returnUrl = null)
    {
        var currentEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(currentEmail)) return Unauthorized();

        Block? block = null;
        if (id.HasValue)
        {
            block = await _context.Blocks.FirstOrDefaultAsync(b => b.Id == id.Value && b.BlockerEmail == currentEmail);
        }
        else if (!string.IsNullOrEmpty(targetEmail))
        {
            block = await _context.Blocks.FirstOrDefaultAsync(b => b.BlockerEmail == currentEmail && b.BlockedEmail == targetEmail);
        }

        if (block != null)
        {
            _context.Blocks.Remove(block);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "已解除封鎖。";
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }
}
