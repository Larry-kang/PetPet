using System.ComponentModel.DataAnnotations;

namespace PetPet.Application.Social;

public class FriendItemDto
{
    public int Id { get; set; }
    public string MemberEmail { get; set; } = string.Empty;
    public Guid MemberPublicId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string? MemberPhoto { get; set; }
    public string? Bio { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public Guid Version { get; set; }
    public bool IsRequester { get; set; }
}

public class FriendListViewModel
{
    public string ActiveTab { get; set; } = "accepted";
    public List<FriendItemDto> AcceptedFriends { get; set; } = [];
    public List<FriendItemDto> ReceivedRequests { get; set; } = [];
    public List<FriendItemDto> SentRequests { get; set; } = [];
}

public class SendFriendRequestDto
{
    [Required(ErrorMessage = "請輸入對象 Email")]
    [EmailAddress(ErrorMessage = "Email 格式無效")]
    public string TargetEmail { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class FriendActionDto
{
    public int Id { get; set; }
    public Guid Version { get; set; }
    public string? ReturnUrl { get; set; }
}
