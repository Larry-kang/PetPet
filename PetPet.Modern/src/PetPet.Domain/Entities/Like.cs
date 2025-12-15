using System.ComponentModel.DataAnnotations;

namespace PetPet.Domain.Entities;

public class Like
{
    public int Id { get; set; }

    public int PostId { get; set; }
    public virtual Post? Post { get; set; }

    [Required]
    public string UserEmail { get; set; } = string.Empty;

    // Navigation
    public virtual Member User { get; set; } = null!;
}
