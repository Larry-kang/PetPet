using System.ComponentModel.DataAnnotations;

namespace PetPet.Application.Members;

public class PublicPetSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KindCode { get; set; } = "Dog";
    public string? VarietyName { get; set; }
    public string? Photo { get; set; }
    public bool? Gender { get; set; }
    public int? EstimatedAgeMonths { get; set; }
    public string? Bio { get; set; }
}

public class PublicPostSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
}

public class PublicMemberProfileDto
{
    public Guid PublicId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? CityName { get; set; }
    public string? Photo { get; set; }
    public List<PublicPetSummaryDto> Pets { get; set; } = [];
    public List<PublicPostSummaryDto> Posts { get; set; } = [];
    public bool IsFriend { get; set; }
    public bool HasPendingRequest { get; set; }
    public bool IsBlocked { get; set; }
    public bool IsSelf { get; set; }
}

public class EditProfileDto
{
    [Required(ErrorMessage = "請輸入姓名")]
    [StringLength(40, MinimumLength = 1, ErrorMessage = "姓名長度需介於 1 至 40 個字元")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "簡介長度不可超過 500 個字元")]
    public string? Bio { get; set; }

    public int? CityId { get; set; }

    [StringLength(1000)]
    public string? Photo { get; set; }

    public bool IsMatchEnabled { get; set; }

    public Guid Version { get; set; }
}

public class ChangePasswordDto
{
    [Required(ErrorMessage = "請輸入目前密碼")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入新密碼")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "新密碼長度需介於 8 至 128 個字元")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "請確認新密碼")]
    [Compare("NewPassword", ErrorMessage = "新密碼與確認密碼不一致")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}
