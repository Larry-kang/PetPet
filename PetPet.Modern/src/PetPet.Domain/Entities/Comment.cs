using System;
using System.ComponentModel.DataAnnotations;

namespace PetPet.Domain.Entities;

public class Comment
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public virtual Post Post { get; set; } = null!;

    public string UserEmail { get; set; } = null!;
    public virtual Member Author { get; set; } = null!;

    [MaxLength(500)]
    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public Guid Version { get; set; } = Guid.NewGuid();
}
