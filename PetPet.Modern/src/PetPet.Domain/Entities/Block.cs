using System;

namespace PetPet.Domain.Entities;

public class Block
{
    public int Id { get; set; }

    public string BlockerEmail { get; set; } = null!;
    public virtual Member Blocker { get; set; } = null!;

    public string BlockedEmail { get; set; } = null!;
    public virtual Member Blocked { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
