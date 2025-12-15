using System.ComponentModel.DataAnnotations;

namespace PetPet.Domain.Entities;

public class Report
{
    public int Id { get; set; }

    public int PostId { get; set; }
    
    // Navigation property
    public virtual Post? Post { get; set; }

    [Required]
    public string ReporterEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending"; // Pending, Resolved, Ignored

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
