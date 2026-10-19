using System;

namespace PetPet.Domain.Entities;

public class OperationReceipt
{
    public int Id { get; set; }

    public string ActorEmail { get; set; } = null!;
    public virtual Member Actor { get; set; } = null!;

    public string Operation { get; set; } = null!;
    public Guid CommandId { get; set; }
    public string PayloadDigest { get; set; } = null!;
    public string ResultCode { get; set; } = null!;
    public string? TargetId { get; set; }
    public DateTime CompletedAtUtc { get; set; }
}
