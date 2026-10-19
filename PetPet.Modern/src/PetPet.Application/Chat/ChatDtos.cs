namespace PetPet.Application.Chat;

public record ChatMessageDto(
    int Id,
    Guid SenderPublicId,
    string SenderName,
    string? SenderPhoto,
    Guid ReceiverPublicId,
    string Content,
    DateTime SentAt,
    DateTime? ReadAt,
    bool IsMine,
    Guid ClientMessageId
);

public record ChatConversationDto(
    Guid TargetPublicId,
    string TargetName,
    string? TargetPhoto,
    bool CanSend,
    string? ReasonCannotSend,
    List<ChatMessageDto> Messages
);

public record ChatContactDto(
    Guid PublicId,
    string Name,
    string? Photo,
    bool CanSend,
    int UnreadCount,
    string? LastMessageSnippet,
    DateTime? LastMessageTime
);

public class SendMessageRequest
{
    public Guid TargetPublicId { get; set; }
    public Guid ClientMessageId { get; set; }
    public string Content { get; set; } = string.Empty;
}

public record SendMessageResponse(
    bool Success,
    int MessageId,
    Guid ClientMessageId,
    DateTime SentAt,
    bool IsDuplicate,
    string? ErrorMessage = null
);

public class MarkReadRequest
{
    public Guid TargetPublicId { get; set; }
    public int UpToMessageId { get; set; }
}
