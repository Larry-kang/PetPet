using System.ComponentModel.DataAnnotations;

namespace PetPet.Application.Pets;

public class CreatePetDto
{
    [Required(ErrorMessage = "請輸入寵物名稱")]
    [StringLength(40, MinimumLength = 1, ErrorMessage = "名稱長度需介於 1 至 40 個字元")]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string KindCode { get; set; } = "Dog";

    public int? VarietyId { get; set; } = 1;

    public bool? Gender { get; set; }

    public DateTime? BirthDate { get; set; }

    [Range(0, 600, ErrorMessage = "估算月齡需介於 0 至 600 個月之間")]
    public int? EstimatedAgeMonths { get; set; }

    public bool IsPublic { get; set; } = true;

    [StringLength(1000)]
    public string? Photo { get; set; } = "/images/presets/avatar-dog.svg";

    [StringLength(500, ErrorMessage = "簡介最多 500 個字元")]
    public string? Bio { get; set; }
}

public class EditPetDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "請輸入寵物名稱")]
    [StringLength(40, MinimumLength = 1, ErrorMessage = "名稱長度需介於 1 至 40 個字元")]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string KindCode { get; set; } = "Dog";

    public int? VarietyId { get; set; }

    public bool? Gender { get; set; }

    public DateTime? BirthDate { get; set; }

    [Range(0, 600, ErrorMessage = "估算月齡需介於 0 至 600 個月之間")]
    public int? EstimatedAgeMonths { get; set; }

    public bool IsPublic { get; set; } = true;

    [StringLength(1000)]
    public string? Photo { get; set; }

    [StringLength(500, ErrorMessage = "簡介最多 500 個字元")]
    public string? Bio { get; set; }

    [Required]
    public Guid Version { get; set; }
}

public class PetDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KindCode { get; set; } = "Dog";
    public string? VarietyName { get; set; }
    public string? Photo { get; set; }
    public bool? Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    public int? EstimatedAgeMonths { get; set; }
    public string? Bio { get; set; }
    public bool IsPublic { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public Guid OwnerPublicId { get; set; }
    public string? OwnerCityName { get; set; }
    public bool IsOwner { get; set; }
    public Guid Version { get; set; }
}
