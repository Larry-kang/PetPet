using System;

namespace PetPet.Domain.Entities;

public class Post
{
    public int Id { get; set; } // Post_no
    public string AuthorEmail { get; set; } = null!; // Post_Email
    public DateTime CreatedAt { get; set; } // Post_time
    public string Title { get; set; } = null!; // Post_title
    public string Content { get; set; } = null!; // Post_content
    public bool IsEnabled { get; set; } // Post_Enable

    // Navigation
    public virtual Member Author { get; set; } = null!;
    public virtual ICollection<Like> Likes { get; set; } = new List<Like>();
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
}
