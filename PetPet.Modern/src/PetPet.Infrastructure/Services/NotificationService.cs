using Microsoft.EntityFrameworkCore;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;

namespace PetPet.Infrastructure.Services;

public interface INotificationService
{
    Task CreateNotificationAsync(
        string recipientEmail,
        string? actorEmail,
        string type,
        string eventKey,
        string targetType,
        int? targetId,
        string summary);
}

public class NotificationService : INotificationService
{
    private readonly PetPetDbContext _context;

    public NotificationService(PetPetDbContext context)
    {
        _context = context;
    }

    public async Task CreateNotificationAsync(
        string recipientEmail,
        string? actorEmail,
        string type,
        string eventKey,
        string targetType,
        int? targetId,
        string summary)
    {
        if (string.IsNullOrEmpty(recipientEmail)) return;
        if (actorEmail != null && string.Equals(recipientEmail, actorEmail, StringComparison.OrdinalIgnoreCase))
        {
            // Do not notify self
            return;
        }

        // Idempotency check with EventKey and Recipient
        bool exists = await _context.Notifications.AnyAsync(n =>
            n.RecipientEmail == recipientEmail && n.EventKey == eventKey);

        if (exists) return;

        var notification = new Notification
        {
            RecipientEmail = recipientEmail,
            ActorEmail = actorEmail,
            Type = type,
            EventKey = eventKey,
            TargetType = targetType,
            TargetId = targetId,
            Summary = summary,
            CreatedAt = DateTime.UtcNow,
            ReadAt = null
        };

        _context.Notifications.Add(notification);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _context.Entry(notification).State = EntityState.Detached;
        }
    }
}
