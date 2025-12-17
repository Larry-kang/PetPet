using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetPet.Domain.Entities;

public class PetVariety
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [ForeignKey("PetType")]
    public int PetTypeId { get; set; }

    public virtual PetType? PetType { get; set; }
}
