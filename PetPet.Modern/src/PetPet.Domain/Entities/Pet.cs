namespace PetPet.Domain.Entities;

public class Pet
{
    public int Id { get; set; } // Pet_no
    public string OwnerEmail { get; set; } = null!;
    public int VarietyId { get; set; } // PetVariety_no
    public string Name { get; set; } = null!;
    public bool Gender { get; set; } // Pet_gender
    public string? Photo { get; set; } // Pet_photo

    // Navigation
    public virtual Member Owner { get; set; } = null!;
}
