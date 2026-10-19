using System;

namespace PetPet.Domain.Entities;

public class Report
{
    public int Id { get; set; }

    public int? PostId { get; set; }
    public virtual Post? Post { get; set; }

    public string ReporterEmail { get; set; } = null!;
    public virtual Member Reporter { get; set; } = null!;

    public string Category { get; set; } = "Other"; // Spam, Harassment, Inappropriate, Other
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Resolved, Dismissed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? HandledAt { get; set; }
    public string? HandledByEmail { get; set; }
    public virtual Member? HandledBy { get; set; }
    public string? HandlingNote { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
