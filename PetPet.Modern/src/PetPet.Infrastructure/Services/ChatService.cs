using Microsoft.EntityFrameworkCore;
using PetPet.Application.Chat;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;

namespace PetPet.Infrastructure.Services;

public class ChatService : IChatService
{
    private readonly PetPetDbContext _db;

    public ChatService(PetPetDbContext db)
    {
        _db = db;
    }

    public static string GetConversationPair(string email1, string email2)
    {
        return string.Compare(email1, email2, StringComparison.Ordinal) < 0
            ? $"{email1}:{email2}"
            : $"{email2}:{email1}";
    }

    public async Task<List<ChatContactDto>> GetContactsAsync(string viewerEmail)
    {
        var viewer = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == viewerEmail);
        if (viewer == null || !viewer.IsEnabled) return new List<ChatContactDto>();

        // 1. Accepted Friends
        var friendEmails = await _db.Friends.AsNoTracking()
            .Where(f => (f.RequesterEmail == viewerEmail || f.AddresseeEmail == viewerEmail) && f.IsAccepted)
            .Select(f => f.RequesterEmail == viewerEmail ? f.AddresseeEmail : f.RequesterEmail)
            .Distinct()
            .ToListAsync();

        // 2. Members with message history
        var messageEmails = await _db.Messages.AsNoTracking()
            .Where(m => m.SenderEmail == viewerEmail || m.ReceiverEmail == viewerEmail)
            .Select(m => m.SenderEmail == viewerEmail ? m.ReceiverEmail : m.SenderEmail)
            .Distinct()
            .ToListAsync();

        var allContactEmails = friendEmails.Union(messageEmails).Distinct().ToList();

        var contacts = await _db.Members.AsNoTracking()
            .Where(m => allContactEmails.Contains(m.Email))
            .ToListAsync();

        var blocks = await _db.Blocks.AsNoTracking()
            .Where(b => (b.BlockerEmail == viewerEmail && allContactEmails.Contains(b.BlockedEmail)) ||
                        (b.BlockedEmail == viewerEmail && allContactEmails.Contains(b.BlockerEmail)))
            .ToListAsync();

        var result = new List<ChatContactDto>();

        foreach (var contact in contacts)
        {
            bool isFriend = friendEmails.Contains(contact.Email);
            bool isBlocked = blocks.Any(b => (b.BlockerEmail == viewerEmail && b.BlockedEmail == contact.Email) ||
                                            (b.BlockerEmail == contact.Email && b.BlockedEmail == viewerEmail));

            bool canSend = isFriend && !isBlocked && contact.IsEnabled;

            var unreadCount = await _db.Messages.AsNoTracking()
                .CountAsync(m => m.SenderEmail == contact.Email && m.ReceiverEmail == viewerEmail && m.ReadAt == null);

            var lastMessage = await _db.Messages.AsNoTracking()
                .Where(m => (m.SenderEmail == viewerEmail && m.ReceiverEmail == contact.Email) ||
                            (m.SenderEmail == contact.Email && m.ReceiverEmail == viewerEmail))
                .OrderByDescending(m => m.SentAt)
                .ThenByDescending(m => m.Id)
                .Select(m => new { m.Content, m.SentAt })
                .FirstOrDefaultAsync();

            result.Add(new ChatContactDto(
                contact.PublicId,
                contact.Name,
                contact.Photo,
                canSend,
                unreadCount,
                lastMessage?.Content,
                lastMessage?.SentAt
            ));
        }

        return result.OrderByDescending(c => c.LastMessageTime ?? DateTime.MinValue)
                     .ThenBy(c => c.Name)
                     .ToList();
    }

    public async Task<ChatConversationDto?> GetConversationAsync(string viewerEmail, Guid targetPublicId)
    {
        var viewer = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == viewerEmail);
        if (viewer == null || !viewer.IsEnabled) return null;

        var target = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.PublicId == targetPublicId);
        if (target == null || target.Email == viewerEmail) return null;

        bool isFriend = await _db.Friends.AsNoTracking()
            .AnyAsync(f => ((f.RequesterEmail == viewerEmail && f.AddresseeEmail == target.Email) ||
                            (f.RequesterEmail == target.Email && f.AddresseeEmail == viewerEmail)) && f.IsAccepted);

        bool isBlocked = await _db.Blocks.AsNoTracking()
            .AnyAsync(b => (b.BlockerEmail == viewerEmail && b.BlockedEmail == target.Email) ||
                           (b.BlockerEmail == target.Email && b.BlockedEmail == viewerEmail));

        bool canSend = isFriend && !isBlocked && target.IsEnabled;

        string? reason = null;
        if (!target.IsEnabled)
        {
            reason = "對方帳號目前處於停權狀態。";
        }
        else if (isBlocked)
        {
            reason = "雙方處於封鎖狀態，無法發送新訊息。";
        }
        else if (!isFriend)
        {
            reason = "僅限已接受之好友傳送訊息。";
        }

        var messages = await GetHistoryAsync(viewerEmail, targetPublicId, limit: 50);

        return new ChatConversationDto(
            target.PublicId,
            target.Name,
            target.Photo,
            canSend,
            reason,
            messages
        );
    }

    public async Task<List<ChatMessageDto>> GetHistoryAsync(string viewerEmail, Guid targetPublicId, DateTime? beforeSentAt = null, int? beforeId = null, int limit = 50)
    {
        var viewer = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == viewerEmail);
        if (viewer == null || !viewer.IsEnabled) return new List<ChatMessageDto>();

        var target = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.PublicId == targetPublicId);
        if (target == null) return new List<ChatMessageDto>();

        var pair = GetConversationPair(viewerEmail, target.Email);

        var query = _db.Messages.AsNoTracking()
            .Where(m => m.ConversationPair == pair);

        if (beforeSentAt.HasValue && beforeId.HasValue)
        {
            query = query.Where(m => m.SentAt < beforeSentAt.Value || (m.SentAt == beforeSentAt.Value && m.Id < beforeId.Value));
        }

        var list = await query
            .OrderByDescending(m => m.SentAt)
            .ThenByDescending(m => m.Id)
            .Take(limit)
            .ToListAsync();

        return list.OrderBy(m => m.SentAt).ThenBy(m => m.Id)
            .Select(m => new ChatMessageDto(
                m.Id,
                m.SenderEmail == viewerEmail ? viewer.PublicId : target.PublicId,
                m.SenderEmail == viewerEmail ? viewer.Name : target.Name,
                m.SenderEmail == viewerEmail ? viewer.Photo : target.Photo,
                m.ReceiverEmail == viewerEmail ? viewer.PublicId : target.PublicId,
                m.Content,
                m.SentAt,
                m.ReadAt,
                m.SenderEmail == viewerEmail,
                m.ClientMessageId
            ))
            .ToList();
    }

    public async Task<SendMessageResponse> SendMessageAsync(string senderEmail, Guid targetPublicId, Guid clientMessageId, string content)
    {
        var sender = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == senderEmail);
        if (sender == null || !sender.IsEnabled)
        {
            return new SendMessageResponse(false, 0, clientMessageId, DateTime.UtcNow, false, "發送者不存在或帳號已停權。");
        }

        var target = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.PublicId == targetPublicId);
        if (target == null || !target.IsEnabled)
        {
            return new SendMessageResponse(false, 0, clientMessageId, DateTime.UtcNow, false, "接收者不存在或帳號已停權。");
        }

        if (sender.Email == target.Email)
        {
            return new SendMessageResponse(false, 0, clientMessageId, DateTime.UtcNow, false, "不可向自己傳送訊息。");
        }

        var trimmed = content?.Trim() ?? string.Empty;
        if (trimmed.Length < 1 || trimmed.Length > 2000)
        {
            return new SendMessageResponse(false, 0, clientMessageId, DateTime.UtcNow, false, "訊息長度必須介於 1 到 2000 字元之間。");
        }

        var pair = GetConversationPair(senderEmail, target.Email);

        // Fast-path idempotency check
        var fastExisting = await _db.Messages.AsNoTracking().FirstOrDefaultAsync(m => m.SenderEmail == senderEmail && m.ClientMessageId == clientMessageId);
        if (fastExisting != null)
        {
            if (fastExisting.ReceiverEmail == target.Email && fastExisting.Content == trimmed)
            {
                return new SendMessageResponse(true, fastExisting.Id, fastExisting.ClientMessageId, fastExisting.SentAt, IsDuplicate: true);
            }
            return new SendMessageResponse(false, 0, clientMessageId, DateTime.UtcNow, false, "ClientMessageId 衝突：相同 ID 但接收者或訊息內容不一致。");
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // Re-verify sender and target enabled status inside transaction
                var currentSender = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == senderEmail);
                if (currentSender == null || !currentSender.IsEnabled)
                {
                    await tx.RollbackAsync();
                    return new SendMessageResponse(false, 0, clientMessageId, DateTime.UtcNow, false, "發送者不存在或帳號已停權。");
                }

                var currentTarget = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == target.Email);
                if (currentTarget == null || !currentTarget.IsEnabled)
                {
                    await tx.RollbackAsync();
                    return new SendMessageResponse(false, 0, clientMessageId, DateTime.UtcNow, false, "接收者不存在或帳號已停權。");
                }

                // Block check inside transaction
                bool isBlocked = await _db.Blocks.AsNoTracking().AnyAsync(b => (b.BlockerEmail == senderEmail && b.BlockedEmail == target.Email) ||
                                                                               (b.BlockerEmail == target.Email && b.BlockedEmail == senderEmail));
                if (isBlocked)
                {
                    await tx.RollbackAsync();
                    return new SendMessageResponse(false, 0, clientMessageId, DateTime.UtcNow, false, "雙方處於封鎖狀態，無法傳送訊息。");
                }

                // Friend check inside transaction
                bool isFriend = await _db.Friends.AsNoTracking().AnyAsync(f => ((f.RequesterEmail == senderEmail && f.AddresseeEmail == target.Email) ||
                                                                                (f.RequesterEmail == target.Email && f.AddresseeEmail == senderEmail)) && f.IsAccepted);
                if (!isFriend)
                {
                    await tx.RollbackAsync();
                    return new SendMessageResponse(false, 0, clientMessageId, DateTime.UtcNow, false, "僅限已接受之好友傳送訊息。");
                }

                // Re-check deduplication inside transaction
                var existingInTx = await _db.Messages.FirstOrDefaultAsync(m => m.SenderEmail == senderEmail && m.ClientMessageId == clientMessageId);
                if (existingInTx != null)
                {
                    await tx.RollbackAsync();
                    if (existingInTx.ReceiverEmail == target.Email && existingInTx.Content == trimmed)
                    {
                        return new SendMessageResponse(true, existingInTx.Id, existingInTx.ClientMessageId, existingInTx.SentAt, IsDuplicate: true);
                    }
                    return new SendMessageResponse(false, 0, clientMessageId, DateTime.UtcNow, false, "ClientMessageId 衝突：相同 ID 但接收者或訊息內容不一致。");
                }

                var msg = new Message
                {
                    SenderEmail = senderEmail,
                    ReceiverEmail = target.Email,
                    ConversationPair = pair,
                    Content = trimmed,
                    SentAt = DateTime.UtcNow,
                    ClientMessageId = clientMessageId
                };

                _db.Messages.Add(msg);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return new SendMessageResponse(true, msg.Id, msg.ClientMessageId, msg.SentAt, IsDuplicate: false);
            }
            catch (DbUpdateException)
            {
                try { await tx.RollbackAsync(); } catch { }

                // Check if race on unique constraint (SenderEmail, ClientMessageId)
                var raced = await _db.Messages.AsNoTracking().FirstOrDefaultAsync(m => m.SenderEmail == senderEmail && m.ClientMessageId == clientMessageId);
                if (raced != null)
                {
                    if (raced.ReceiverEmail == target.Email && raced.Content == trimmed)
                    {
                        return new SendMessageResponse(true, raced.Id, raced.ClientMessageId, raced.SentAt, IsDuplicate: true);
                    }
                    return new SendMessageResponse(false, 0, clientMessageId, DateTime.UtcNow, false, "ClientMessageId 衝突：相同 ID 但接收者或訊息內容不一致。");
                }

                return new SendMessageResponse(false, 0, clientMessageId, DateTime.UtcNow, false, "資料庫處理衝突，請稍後重試。");
            }
            catch (Exception ex) when (ex is DbUpdateConcurrencyException || ex.GetType().Name.Contains("SqliteException"))
            {
                try { await tx.RollbackAsync(); } catch { }
                return new SendMessageResponse(false, 0, clientMessageId, DateTime.UtcNow, false, "資料庫忙碌或並發衝突，請稍後重試。");
            }
        });
    }

    public async Task<int> MarkReadAsync(string viewerEmail, Guid targetPublicId, int upToMessageId)
    {
        var viewer = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == viewerEmail);
        if (viewer == null || !viewer.IsEnabled) return 0;

        var target = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.PublicId == targetPublicId);
        if (target == null) return 0;

        bool isBlocked = await _db.Blocks.AsNoTracking().AnyAsync(b =>
            (b.BlockerEmail == viewerEmail && b.BlockedEmail == target.Email) ||
            (b.BlockerEmail == target.Email && b.BlockedEmail == viewerEmail));
        if (isBlocked) return 0;

        var unread = await _db.Messages
            .Where(m => m.ReceiverEmail == viewerEmail && m.SenderEmail == target.Email && m.Id <= upToMessageId && m.ReadAt == null)
            .ToListAsync();

        if (unread.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var m in unread)
        {
            m.ReadAt = now;
        }

        await _db.SaveChangesAsync();
        return unread.Count;
    }
}
