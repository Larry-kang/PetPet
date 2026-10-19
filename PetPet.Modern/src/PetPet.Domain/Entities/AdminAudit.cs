using System;

namespace PetPet.Domain.Entities;

public class AdminAudit
{
    public int Id { get; set; }

    public string ActorEmail { get; set; } = null!;
    public virtual Member Actor { get; set; } = null!;

    public string TargetId { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string TraceId { get; set; } = null!;
}
