using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetPet.Domain.Entities;

public class Message
{
    public int Id { get; set; }

    [Required]
    public string SenderEmail { get; set; } = null!;

    [ForeignKey("SenderEmail")]
    public virtual Member Sender { get; set; } = null!;

    [Required]
    public string ReceiverEmail { get; set; } = null!;

    [ForeignKey("ReceiverEmail")]
    public virtual Member Receiver { get; set; } = null!;

    [Required]
    public string Content { get; set; } = "";
    
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}
