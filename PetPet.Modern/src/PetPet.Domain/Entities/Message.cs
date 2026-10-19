using System;

namespace PetPet.Domain.Entities;

public class Message
{
    public int Id { get; set; }

    public string SenderEmail { get; set; } = null!;
    public virtual Member Sender { get; set; } = null!;

    public string ReceiverEmail { get; set; } = null!;
    public virtual Member Receiver { get; set; } = null!;

    public string ConversationPair { get; set; } = null!;
    public string Content { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public bool IsRead { get => ReadAt.HasValue; set { ReadAt = value ? (ReadAt ?? DateTime.UtcNow) : null; } }
    public Guid ClientMessageId { get; set; } = Guid.NewGuid();
}
