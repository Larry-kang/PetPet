using System.ComponentModel.DataAnnotations;

namespace PetPet.Domain.Entities;

public class Comment
{
    public int Id { get; set; }

    public int PostId { get; set; }
    // Navigation property
    public virtual Post? Post { get; set; }

    // In legacy this was 'Email'
    [Required]
    public string UserEmail { get; set; } = string.Empty;
    
    // Navigation to Author (Member) - handy for displaying name/photo
    public virtual Member? Author { get; set; }

    [Required]
    [MaxLength(500)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
