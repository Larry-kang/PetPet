using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PetPet.Application.Social;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;
using PetPet.Infrastructure.Services;

namespace PetPet.Web.Controllers;

[Authorize]
public class FriendController : Controller
{
    private readonly PetPetDbContext _context;
    private readonly INotificationService _notifications;

    public FriendController(PetPetDbContext context, INotificationService notifications)
    {
        _context = context;
        _notifications = notifications;
    }

    // GET: /Friend/Index
    [HttpGet]
    public async Task<IActionResult> Index(string tab = "accepted")
    {
        var currentEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(currentEmail)) return Unauthorized();

        var query = _context.Friends.AsNoTracking()
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .Where(f => f.RequesterEmail == currentEmail || f.AddresseeEmail == currentEmail);

        var allRecords = await query.ToListAsync();

        var accepted = allRecords
            .Where(f => f.IsAccepted)
            .OrderByDescending(f => f.AcceptedAtUtc ?? f.CreatedAt)
            .Select(f =>
            {
                var other = f.RequesterEmail == currentEmail ? f.Addressee : f.Requester;
                return new FriendItemDto
                {
                    Id = f.Id,
                    MemberEmail = other.Email,
                    MemberPublicId = other.PublicId,
                    MemberName = other.Name,
                    MemberPhoto = other.Photo,
                    Bio = other.Bio,
                    CreatedAt = f.CreatedAt,
                    AcceptedAtUtc = f.AcceptedAtUtc,
                    Version = f.Version,
                    IsRequester = f.RequesterEmail == currentEmail
                };
            })
            .ToList();

        var received = allRecords
            .Where(f => !f.IsAccepted && f.AddresseeEmail == currentEmail)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FriendItemDto
            {
                Id = f.Id,
                MemberEmail = f.Requester.Email,
                MemberPublicId = f.Requester.PublicId,
                MemberName = f.Requester.Name,
                MemberPhoto = f.Requester.Photo,
                Bio = f.Requester.Bio,
                CreatedAt = f.CreatedAt,
                Version = f.Version,
                IsRequester = false
            })
            .ToList();

        var sent = allRecords
            .Where(f => !f.IsAccepted && f.RequesterEmail == currentEmail)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FriendItemDto
            {
                Id = f.Id,
                MemberEmail = f.Addressee.Email,
                MemberPublicId = f.Addressee.PublicId,
                MemberName = f.Addressee.Name,
                MemberPhoto = f.Addressee.Photo,
                Bio = f.Addressee.Bio,
                CreatedAt = f.CreatedAt,
                Version = f.Version,
                IsRequester = true
            })
            .ToList();

        var viewModel = new FriendListViewModel
        {
            ActiveTab = tab.ToLowerInvariant() switch
            {
                "received" => "received",
                "sent" => "sent",
                _ => "accepted"
            },
            AcceptedFriends = accepted,
            ReceivedRequests = received,
            SentRequests = sent
        };

        return View(viewModel);
    }

    // POST: /Friend/SendRequest
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("friend-limit")]
    public async Task<IActionResult> SendRequest(string targetEmail, string? returnUrl = null)
    {
        var currentEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(currentEmail)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(targetEmail))
        {
            TempData["ErrorMessage"] = "目標會員 Email 不可為空。";
            return SafeRedirect(returnUrl);
        }

        targetEmail = targetEmail.Trim().ToLowerInvariant();
        currentEmail = currentEmail.Trim().ToLowerInvariant();

        if (string.Equals(targetEmail, currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "不能向自己發送好友邀請。";
            return BadRequest("不能向自己發送好友邀請。");
        }

        var targetMember = await _context.Members.AsNoTracking().FirstOrDefaultAsync(m => m.NormalizedEmail == targetEmail.ToUpperInvariant());
        if (targetMember == null || !targetMember.IsEnabled)
        {
            TempData["ErrorMessage"] = "找不到指定的會員或該帳號已被停用。";
            return NotFound("找不到指定的會員。");
        }

        // Mutual Block check
        var isBlocked = await _context.Blocks.AsNoTracking().AnyAsync(b =>
            (b.BlockerEmail == currentEmail && b.BlockedEmail == targetMember.Email) ||
            (b.BlockerEmail == targetMember.Email && b.BlockedEmail == currentEmail));

        if (isBlocked)
        {
            TempData["ErrorMessage"] = "無法與該會員建立好友關係。";
            return StatusCode(StatusCodes.Status409Conflict, "無法與該會員建立好友關係。");
        }

        string memberA = string.CompareOrdinal(currentEmail, targetMember.Email) < 0 ? currentEmail : targetMember.Email;
        string memberB = string.CompareOrdinal(currentEmail, targetMember.Email) < 0 ? targetMember.Email : currentEmail;

        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var isBlockedNow = await _context.Blocks.AnyAsync(b =>
                (b.BlockerEmail == currentEmail && b.BlockedEmail == targetMember.Email) ||
                (b.BlockerEmail == targetMember.Email && b.BlockedEmail == currentEmail));
            if (isBlockedNow)
            {
                await tx.RollbackAsync();
                return StatusCode(StatusCodes.Status409Conflict, "無法與該會員建立好友關係。");
            }

            var existingFriend = await _context.Friends.FirstOrDefaultAsync(f => f.MemberAEmail == memberA && f.MemberBEmail == memberB);
            if (existingFriend != null)
            {
                await tx.RollbackAsync();
                if (existingFriend.IsAccepted)
                {
                    TempData["ErrorMessage"] = "你們已經是好友了。";
                    return SafeRedirect(returnUrl);
                }

                if (existingFriend.RequesterEmail == currentEmail)
                {
                    TempData["ErrorMessage"] = "您先前已發送好友邀請，請等待對方確認。";
                    return SafeRedirect(returnUrl);
                }

                TempData["ErrorMessage"] = "對方先前已向您發送好友邀請，請前往邀請列表確認接受。";
                return StatusCode(StatusCodes.Status409Conflict, "對方已經向您發送好友邀請，請直接接受。");
            }

            var newFriend = new Friend
            {
                RequesterEmail = currentEmail,
                AddresseeEmail = targetMember.Email,
                MemberAEmail = memberA,
                MemberBEmail = memberB,
                IsAccepted = false,
                CreatedAt = DateTime.UtcNow,
                Version = Guid.NewGuid()
            };

            _context.Friends.Add(newFriend);
            await _context.SaveChangesAsync();

            var currentName = User.Identity?.Name ?? "會員";
            await _notifications.CreateNotificationAsync(
                targetMember.Email,
                currentEmail,
                "friend_requested",
                $"friend_req:{newFriend.Id}",
                "Friend",
                newFriend.Id,
                $"{currentName} 向您發送了好友邀請。");

            await tx.CommitAsync();
            TempData["SuccessMessage"] = $"已成功向 {targetMember.Name} 送出好友邀請！";
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync();
            TempData["ErrorMessage"] = "發送好友邀請時發生並發衝突，請重新整理後再試。";
            return StatusCode(StatusCodes.Status409Conflict, "並發好友邀請衝突。");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return SafeRedirect(returnUrl);
    }

    // POST: /Friend/Send (alias for SendRequest)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Send(string targetEmail, string? returnUrl = null)
    {
        return SendRequest(targetEmail, returnUrl);
    }

    // POST: /Friend/Accept
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id, Guid version, string? returnUrl = null)
    {
        var currentEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(currentEmail)) return Unauthorized();

        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var friend = await _context.Friends.FirstOrDefaultAsync(f => f.Id == id);
            if (friend == null)
            {
                await tx.RollbackAsync();
                return NotFound("找不到指定的好友邀請。");
            }

            if (!string.Equals(friend.AddresseeEmail, currentEmail, StringComparison.OrdinalIgnoreCase))
            {
                await tx.RollbackAsync();
                return Forbid();
            }

            if (friend.IsAccepted)
            {
                await tx.RollbackAsync();
                TempData["SuccessMessage"] = "已經是好友關係。";
                return SafeRedirect(returnUrl);
            }

            if (friend.Version != version)
            {
                await tx.RollbackAsync();
                TempData["ErrorMessage"] = "好友邀請已被其他操作變更，請重新整理。";
                return StatusCode(StatusCodes.Status409Conflict, "版本衝突。");
            }

            // Concurrency check against Block in transaction
            var isBlocked = await _context.Blocks.AnyAsync(b =>
                (b.BlockerEmail == currentEmail && b.BlockedEmail == friend.RequesterEmail) ||
                (b.BlockerEmail == friend.RequesterEmail && b.BlockedEmail == currentEmail));
            if (isBlocked)
            {
                await tx.RollbackAsync();
                return StatusCode(StatusCodes.Status409Conflict, "雙方處於封鎖狀態，無法接受好友邀請。");
            }

            friend.IsAccepted = true;
            friend.AcceptedAtUtc = DateTime.UtcNow;
            friend.Version = Guid.NewGuid();

            await _context.SaveChangesAsync();

            var currentName = User.Identity?.Name ?? "會員";
            await _notifications.CreateNotificationAsync(
                friend.RequesterEmail,
                currentEmail,
                "friend_accepted",
                $"friend_acc:{friend.Id}",
                "Friend",
                friend.Id,
                $"{currentName} 接受了您的好友邀請，現在可以開始私訊囉！");

            await tx.CommitAsync();
            TempData["SuccessMessage"] = "已接受好友邀請！現在可以開始傳送私訊了。";
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            TempData["ErrorMessage"] = "好友邀請已被其他操作變更，請重新整理。";
            return StatusCode(StatusCodes.Status409Conflict, "版本衝突。");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return SafeRedirect(returnUrl);
    }

    // POST: /Friend/Reject
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, Guid version, string? returnUrl = null)
    {
        var currentEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(currentEmail)) return Unauthorized();

        var friend = await _context.Friends.FirstOrDefaultAsync(f => f.Id == id);
        if (friend == null) return NotFound("找不到指定的好友邀請。");

        if (!string.Equals(friend.AddresseeEmail, currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (friend.Version != version)
        {
            TempData["ErrorMessage"] = "好友邀請已被其他操作變更，請重新整理。";
            return StatusCode(StatusCodes.Status409Conflict, "版本衝突。");
        }

        _context.Friends.Remove(friend);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["ErrorMessage"] = "好友邀請已被其他操作變更，請重新整理。";
            return StatusCode(StatusCodes.Status409Conflict, "版本衝突。");
        }

        TempData["SuccessMessage"] = "已拒絕該好友邀請。";
        return SafeRedirect(returnUrl);
    }

    // POST: /Friend/Cancel
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, Guid version, string? returnUrl = null)
    {
        var currentEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(currentEmail)) return Unauthorized();

        var friend = await _context.Friends.FirstOrDefaultAsync(f => f.Id == id);
        if (friend == null) return NotFound("找不到指定的好友邀請。");

        if (!string.Equals(friend.RequesterEmail, currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (friend.IsAccepted)
        {
            TempData["ErrorMessage"] = "對方已接受好友邀請，無法取消。";
            return BadRequest("對方已接受好友邀請，無法取消。");
        }

        if (friend.Version != version)
        {
            TempData["ErrorMessage"] = "好友邀請狀態已被變更，請重新整理。";
            return StatusCode(StatusCodes.Status409Conflict, "版本衝突。");
        }

        _context.Friends.Remove(friend);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["ErrorMessage"] = "好友邀請已被其他操作變更，請重新整理。";
            return StatusCode(StatusCodes.Status409Conflict, "版本衝突。");
        }

        TempData["SuccessMessage"] = "已取消發出的好友邀請。";
        return SafeRedirect(returnUrl);
    }

    // POST: /Friend/Remove
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id, Guid version, string? returnUrl = null)
    {
        var currentEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(currentEmail)) return Unauthorized();

        var friend = await _context.Friends.FirstOrDefaultAsync(f => f.Id == id);
        if (friend == null) return NotFound("找不到指定的好友關係。");

        if (!string.Equals(friend.RequesterEmail, currentEmail, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(friend.AddresseeEmail, currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (friend.Version != version)
        {
            TempData["ErrorMessage"] = "好友關係狀態已被變更，請重新整理。";
            return StatusCode(StatusCodes.Status409Conflict, "版本衝突。");
        }

        _context.Friends.Remove(friend);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["ErrorMessage"] = "好友關係已被其他操作變更，請重新整理。";
            return StatusCode(StatusCodes.Status409Conflict, "版本衝突。");
        }

        TempData["SuccessMessage"] = "已解除好友關係。";
        return SafeRedirect(returnUrl);
    }

    private IActionResult SafeRedirect(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction(nameof(Index));
    }
}
