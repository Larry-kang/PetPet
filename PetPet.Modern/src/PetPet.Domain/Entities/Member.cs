using System;
using System.Collections.Generic;

namespace PetPet.Domain.Entities;

public class Member
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!; // Pwd
    public string Name { get; set; } = null!;
    public DateTime Birthday { get; set; }
    public bool Gender { get; set; }
    public string Phone { get; set; } = null!;
    public int CityId { get; set; } // City_no
    public string? Photo { get; set; } // Mem_photo
    public bool IsEnabled { get; set; } // Enable
    public bool IsMatchEnabled { get; set; } // Match_Enable

    // Navigation
    public virtual ICollection<Pet> Pets { get; set; } = new List<Pet>();
    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
}
