using System;

namespace PetPet.Domain.Entities;

public class Notification
{
    public int Id { get; set; }

    public string RecipientEmail { get; set; } = null!;
    public virtual Member Recipient { get; set; } = null!;

    public string? ActorEmail { get; set; }
    public virtual Member? Actor { get; set; }

    public string Type { get; set; } = null!; // "friend_requested", "friend_accepted", "comment_created"
    public string EventKey { get; set; } = null!;
    public string TargetType { get; set; } = null!;
    public Guid? TargetPublicId { get; set; }
    public int? TargetId { get; set; }
    public string Summary { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
