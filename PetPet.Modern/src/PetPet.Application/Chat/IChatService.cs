namespace PetPet.Application.Chat;

public interface IChatService
{
    Task<List<ChatContactDto>> GetContactsAsync(string viewerEmail);
    Task<ChatConversationDto?> GetConversationAsync(string viewerEmail, Guid targetPublicId);
    Task<List<ChatMessageDto>> GetHistoryAsync(string viewerEmail, Guid targetPublicId, DateTime? beforeSentAt = null, int? beforeId = null, int limit = 50);
    Task<SendMessageResponse> SendMessageAsync(string senderEmail, Guid targetPublicId, Guid clientMessageId, string content);
    Task<int> MarkReadAsync(string viewerEmail, Guid targetPublicId, int upToMessageId);
}
