using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetPet.Domain.Entities;

public class Friend
{
    public int Id { get; set; }

    [Required]
    public string RequesterEmail { get; set; } = null!;
    
    [ForeignKey("RequesterEmail")]
    public virtual Member Requester { get; set; } = null!;

    [Required]
    public string AddresseeEmail { get; set; } = null!;

    [ForeignKey("AddresseeEmail")]
    public virtual Member Addressee { get; set; } = null!;

    public bool IsAccepted { get; set; } // false = pending, true = friend
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
