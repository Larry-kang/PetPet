using System.ComponentModel.DataAnnotations;

namespace PetPet.Domain.Entities;

public class PetType
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    public virtual ICollection<PetVariety> Varieties { get; set; } = new List<PetVariety>();
}
