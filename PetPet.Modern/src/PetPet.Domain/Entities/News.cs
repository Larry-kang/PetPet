using System.ComponentModel.DataAnnotations;

namespace PetPet.Domain.Entities;

public class News
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public string? PhotoUrl { get; set; }

    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? ExpiresAt { get; set; }

    // Navigation properties can be added if we have Admin entity, 
    // but for now we can simplify or just store AdminName if needed.
    // Legacy had Admin_no.
}
