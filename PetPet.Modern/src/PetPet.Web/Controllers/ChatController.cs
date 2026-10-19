using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PetPet.Application.Chat;
using PetPet.Infrastructure.Data;
using PetPet.Web.Hubs;
using System.Security.Claims;

namespace PetPet.Web.Controllers;

[Authorize]
public class ChatController : Controller
{
    private readonly IChatService _chatService;
    private readonly PetPetDbContext _db;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IChatRateLimiter _chatRateLimiter;

    public ChatController(IChatService chatService, PetPetDbContext db, IHubContext<ChatHub> hubContext, IChatRateLimiter chatRateLimiter)
    {
        _chatService = chatService;
        _db = db;
        _hubContext = hubContext;
        _chatRateLimiter = chatRateLimiter;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? with)
    {
        var currentEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentEmail)) return RedirectToAction("Login", "Member");

        var viewer = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == currentEmail);
        if (viewer == null || !viewer.IsEnabled)
        {
            return Forbid();
        }

        var contacts = await _chatService.GetContactsAsync(currentEmail);
        ViewBag.Contacts = contacts;
        ViewBag.ViewerPublicId = viewer.PublicId;

        if (with.HasValue)
        {
            var conversation = await _chatService.GetConversationAsync(currentEmail, with.Value);
            if (conversation != null)
            {
                ViewBag.ActiveConversation = conversation;
                // Auto mark as read on view
                if (conversation.Messages.Count > 0)
                {
                    var lastId = conversation.Messages.Max(m => m.Id);
                    await _chatService.MarkReadAsync(currentEmail, with.Value, lastId);
                }
            }
        }

        return View(contacts);
    }

    [HttpGet]
    public async Task<IActionResult> History(Guid targetPublicId, DateTime? beforeSentAt, int? beforeId, int limit = 50)
    {
        var currentEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentEmail)) return Unauthorized();

        var history = await _chatService.GetHistoryAsync(currentEmail, targetPublicId, beforeSentAt, beforeId, limit);
        return Json(history);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest request)
    {
        var currentEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentEmail)) return Unauthorized();

        if (request == null) return BadRequest("無效的訊息請求。");

        var limitResult = _chatRateLimiter.TryAcquire(currentEmail);
        if (!limitResult.IsAllowed)
        {
            Response.Headers.RetryAfter = limitResult.RetryAfterSeconds.ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests, new SendMessageResponse(
                Success: false,
                MessageId: 0,
                ClientMessageId: request.ClientMessageId,
                SentAt: DateTime.UtcNow,
                IsDuplicate: false,
                ErrorMessage: $"傳送訊息過於頻繁，已達每分鐘 60 則上限，請稍後再試 (429 Too Many Requests)。"
            ));
        }

        var result = await _chatService.SendMessageAsync(currentEmail, request.TargetPublicId, request.ClientMessageId, request.Content);

        if (!result.Success)
        {
            if (result.ErrorMessage?.Contains("衝突") == true)
            {
                return Conflict(result);
            }
            return BadRequest(result);
        }

        // Notify receiver via SignalR
        var target = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.PublicId == request.TargetPublicId);
        var sender = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == currentEmail);
        if (!result.IsDuplicate && target != null && sender != null)
        {
            await _hubContext.Clients.User(target.Email).SendAsync("ReceiveMessage", new
            {
                Id = result.MessageId,
                SenderPublicId = sender.PublicId,
                SenderName = sender.Name,
                SenderPhoto = sender.Photo,
                Content = request.Content.Trim(),
                SentAt = result.SentAt,
                ClientMessageId = result.ClientMessageId
            });
        }

        return Ok(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead([FromBody] MarkReadRequest request)
    {
        var currentEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentEmail)) return Unauthorized();

        if (request == null) return BadRequest();

        int count = await _chatService.MarkReadAsync(currentEmail, request.TargetPublicId, request.UpToMessageId);

        if (count > 0)
        {
            var target = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.PublicId == request.TargetPublicId);
            var viewer = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == currentEmail);
            if (target != null && viewer != null)
            {
                await _hubContext.Clients.User(target.Email).SendAsync("MessagesRead", new
                {
                    ReaderPublicId = viewer.PublicId,
                    UpToMessageId = request.UpToMessageId,
                    ReadAt = DateTime.UtcNow
                });
            }
        }

        return Ok(new { Count = count });
    }
}
