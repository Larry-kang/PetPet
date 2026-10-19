using System;

namespace PetPet.Domain.Entities;

public class Friend
{
    public int Id { get; set; }

    public string RequesterEmail { get; set; } = null!;
    public virtual Member Requester { get; set; } = null!;

    public string AddresseeEmail { get; set; } = null!;
    public virtual Member Addressee { get; set; } = null!;

    // Normalized pair
    public string MemberAEmail { get; set; } = null!;
    public string MemberBEmail { get; set; } = null!;

    public bool IsAccepted { get; set; } // false = pending, true = friend
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAtUtc { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
