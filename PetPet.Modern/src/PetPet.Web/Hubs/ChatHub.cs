using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PetPet.Application.Chat;
using PetPet.Infrastructure.Data;

namespace PetPet.Web.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly PetPetDbContext _db;
    private readonly IChatRateLimiter _chatRateLimiter;
    private static IChatRateLimiter? _staticRateLimiter;

    public ChatHub(IChatService chatService, PetPetDbContext db, IChatRateLimiter chatRateLimiter)
    {
        _chatService = chatService;
        _db = db;
        _chatRateLimiter = chatRateLimiter;
        _staticRateLimiter = chatRateLimiter;
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext != null)
        {
            var origin = httpContext.Request.Headers["Origin"].ToString();
            if (!string.IsNullOrEmpty(origin))
            {
                if (Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
                {
                    var host = httpContext.Request.Host.Host;
                    if (!string.Equals(originUri.Host, host, StringComparison.OrdinalIgnoreCase))
                    {
                        Context.Abort();
                        return;
                    }

                    if (httpContext.Request.Host.Port.HasValue && originUri.Port != httpContext.Request.Host.Port.Value)
                    {
                        Context.Abort();
                        return;
                    }
                }
                else
                {
                    Context.Abort();
                    return;
                }
            }
        }

        var senderEmail = Context.UserIdentifier;
        var stamp = Context.User?.FindFirst("SecurityStamp")?.Value;
        if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(stamp))
        {
            Context.Abort();
            return;
        }

        var member = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == senderEmail);
        if (member == null || !member.IsEnabled || member.SecurityStamp != stamp)
        {
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }

    public static void ResetRateLimits() => _staticRateLimiter?.Reset();

    public async Task SendMessage(Guid targetPublicId, Guid clientMessageId, string content)
    {
        var senderEmail = Context.UserIdentifier;
        var stamp = Context.User?.FindFirst("SecurityStamp")?.Value;
        if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(stamp))
        {
            Context.Abort();
            return;
        }

        var sender = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == senderEmail);
        if (sender == null || !sender.IsEnabled || memberSecurityStampMismatch(sender.SecurityStamp, stamp))
        {
            await Clients.Caller.SendAsync("MessageFailed", new
            {
                ClientMessageId = clientMessageId,
                Error = "身分驗證失效或帳號已停權，連線即將中斷。"
            });
            Context.Abort();
            return;
        }

        // PP-SEC-04: Unified chat rate limit 60/min per member (shared across HTTP & Hub)
        var limitResult = _chatRateLimiter.TryAcquire(senderEmail);
        if (!limitResult.IsAllowed)
        {
            await Clients.Caller.SendAsync("MessageFailed", new
            {
                ClientMessageId = clientMessageId,
                Error = "傳送訊息過於頻繁，已達每分鐘 60 則上限，請稍後再試 (429 Too Many Requests)。",
                RetryAfter = limitResult.RetryAfterSeconds
            });
            return;
        }

        var res = await _chatService.SendMessageAsync(senderEmail, targetPublicId, clientMessageId, content);
        if (res.Success)
        {
            var target = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.PublicId == targetPublicId);

            // Ack to caller (indicates if duplicate)
            await Clients.Caller.SendAsync("MessageAck", res);

            // Replay must NOT push another message to receiver
            if (!res.IsDuplicate && target != null)
            {
                await Clients.User(target.Email).SendAsync("ReceiveMessage", new
                {
                    Id = res.MessageId,
                    SenderPublicId = sender.PublicId,
                    SenderName = sender.Name,
                    SenderPhoto = sender.Photo,
                    Content = content.Trim(),
                    SentAt = res.SentAt,
                    ClientMessageId = res.ClientMessageId
                });
            }
        }
        else
        {
            await Clients.Caller.SendAsync("MessageFailed", new
            {
                ClientMessageId = clientMessageId,
                Error = res.ErrorMessage
            });
        }
    }

    public async Task MarkRead(Guid targetPublicId, int upToMessageId)
    {
        var viewerEmail = Context.UserIdentifier;
        var stamp = Context.User?.FindFirst("SecurityStamp")?.Value;
        if (string.IsNullOrEmpty(viewerEmail) || string.IsNullOrEmpty(stamp))
        {
            Context.Abort();
            return;
        }

        var viewer = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == viewerEmail);
        if (viewer == null || !viewer.IsEnabled || memberSecurityStampMismatch(viewer.SecurityStamp, stamp))
        {
            Context.Abort();
            return;
        }

        int count = await _chatService.MarkReadAsync(viewerEmail, targetPublicId, upToMessageId);
        if (count > 0)
        {
            var target = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.PublicId == targetPublicId);
            if (target != null)
            {
                await Clients.User(target.Email).SendAsync("MessagesRead", new
                {
                    ReaderPublicId = viewer.PublicId,
                    UpToMessageId = upToMessageId,
                    ReadAt = DateTime.UtcNow
                });
            }
        }
    }

    private static bool memberSecurityStampMismatch(string? actualStamp, string? claimStamp)
    {
        return !string.Equals(actualStamp, claimStamp, StringComparison.Ordinal);
    }
}
