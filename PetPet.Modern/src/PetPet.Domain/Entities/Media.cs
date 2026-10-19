using System;

namespace PetPet.Domain.Entities;

public class Media
{
    public Guid Id { get; set; }

    public string OwnerEmail { get; set; } = null!;
    public virtual Member Owner { get; set; } = null!;

    public string StorageKey { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long ByteLength { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? OrphanedAt { get; set; }
}
