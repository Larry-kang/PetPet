using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetPet.Domain.Entities;

public enum MatchAction
{
    Pass = 0,
    Like = 1
}

public class MatchInteraction
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string SourceMemberId { get; set; } = null!; // Email

    [Required]
    public string TargetMemberId { get; set; } = null!; // Email

    public MatchAction Action { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
