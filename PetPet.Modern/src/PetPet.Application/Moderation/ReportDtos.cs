using System.ComponentModel.DataAnnotations;

namespace PetPet.Application.Moderation;

public class CreateReportDto
{
    [Required]
    public int PostId { get; set; }

    [Required(ErrorMessage = "請選擇檢舉類別")]
    public string Category { get; set; } = "Spam"; // Spam, Harassment, Inappropriate, Other

    [StringLength(500, ErrorMessage = "補充說明長度不可超過 500 個字元")]
    public string? Reason { get; set; }
}

public class ReportItemDto
{
    public int Id { get; set; }
    public int? PostId { get; set; }
    public string PostTitle { get; set; } = string.Empty;
    public string PostAuthorName { get; set; } = string.Empty;
    public bool PostIsEnabled { get; set; }
    public bool PostIsDeleted { get; set; }
    public string ReporterEmail { get; set; } = string.Empty;
    public string ReporterName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? HandledAt { get; set; }
    public string? HandledByName { get; set; }
    public string? HandlingNote { get; set; }
    public Guid Version { get; set; }
}

public class AdminReportsViewModel
{
    public string Status { get; set; } = "Pending";
    public int PendingCount { get; set; }
    public int ResolvedCount { get; set; }
    public int DismissedCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalItems { get; set; }
    public bool HasPrevious => CurrentPage > 1;
    public bool HasNext => CurrentPage < TotalPages;
    public List<ReportItemDto> Reports { get; set; } = [];
}
