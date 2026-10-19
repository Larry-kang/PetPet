using System;

namespace PetPet.Domain.Entities;

public class Pet
{
    public int Id { get; set; } // Pet_no
    public string OwnerEmail { get; set; } = null!;
    public int? VarietyId { get; set; }
    public string KindCode { get; set; } = "Dog"; // "Dog", "Cat", "Other"
    public string Name { get; set; } = null!;
    public bool? Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    public int? EstimatedAgeMonths { get; set; }
    public string? Bio { get; set; }
    public bool IsPublic { get; set; } = true;
    public string? Photo { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();

    // Navigation
    public virtual Member Owner { get; set; } = null!;
}
