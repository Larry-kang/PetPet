using System.ComponentModel.DataAnnotations;

namespace PetPet.Application.Social;

public class PostFeedItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public Guid AuthorPublicId { get; set; }
    public string? AuthorPhoto { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public bool ViewerHasLiked { get; set; }
}

public class PostFeedViewModel
{
    public List<PostFeedItemDto> Items { get; set; } = [];
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalItems { get; set; }
    public bool HasPrevious => CurrentPage > 1;
    public bool HasNext => CurrentPage < TotalPages;
}

public class CommentItemDto
{
    public int Id { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public Guid AuthorPublicId { get; set; }
    public string? AuthorPhoto { get; set; }
    public string AuthorEmail { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid Version { get; set; }
    public bool IsMyComment { get; set; }
}

public class PostDetailDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public Guid AuthorPublicId { get; set; }
    public string? AuthorPhoto { get; set; }
    public Guid Version { get; set; }
    public int LikeCount { get; set; }
    public bool ViewerHasLiked { get; set; }
    public bool IsMyPost { get; set; }

    public List<CommentItemDto> Comments { get; set; } = [];
    public int CommentPage { get; set; } = 1;
    public int TotalCommentPages { get; set; } = 1;
    public int TotalComments { get; set; }
}

public class CreatePostDto
{
    [Required(ErrorMessage = "請輸入貼文標題")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "標題長度需介於 1 至 100 個字元")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入貼文內容")]
    [StringLength(4000, MinimumLength = 1, ErrorMessage = "內容長度需介於 1 至 4000 個字元")]
    public string Content { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? ImageUrl { get; set; }

    public Guid? CommandId { get; set; }
}

public class EditPostDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "請輸入貼文標題")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "標題長度需介於 1 至 100 個字元")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入貼文內容")]
    [StringLength(4000, MinimumLength = 1, ErrorMessage = "內容長度需介於 1 至 4000 個字元")]
    public string Content { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? ImageUrl { get; set; }

    [Required]
    public Guid Version { get; set; }
}
