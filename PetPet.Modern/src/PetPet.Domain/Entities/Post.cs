using System;
using System.Collections.Generic;

namespace PetPet.Domain.Entities;

public class Post
{
    public int Id { get; set; } // Post_no
    public string AuthorEmail { get; set; } = null!; // Post_Email
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Post_time
    public DateTime? UpdatedAt { get; set; }
    public string Title { get; set; } = null!; // Post_title
    public string Content { get; set; } = null!; // Post_content
    public string? ImageUrl { get; set; } // Post_img
    public bool IsEnabled { get; set; } = true; // Post_Enable
    public bool IsDeleted { get; set; } = false;
    public Guid Version { get; set; } = Guid.NewGuid();

    // Navigation
    public virtual Member Author { get; set; } = null!;
    public virtual ICollection<Like> Likes { get; set; } = new List<Like>();
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
}
