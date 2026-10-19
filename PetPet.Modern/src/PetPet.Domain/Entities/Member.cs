using System;
using System.Collections.Generic;

namespace PetPet.Domain.Entities;

public class Member
{
    public string Email { get; set; } = null!;
    public string NormalizedEmail { get; set; } = string.Empty;
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public string Password { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateTime? Birthday { get; set; }
    public bool? Gender { get; set; }
    public string? Phone { get; set; }
    public int? CityId { get; set; }
    public string? Bio { get; set; }
    public string? Photo { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsMatchEnabled { get; set; }
    public bool IsAdmin { get; set; }
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public Guid Version { get; set; } = Guid.NewGuid();

    // Navigation
    public virtual ICollection<Pet> Pets { get; set; } = new List<Pet>();
    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
}
