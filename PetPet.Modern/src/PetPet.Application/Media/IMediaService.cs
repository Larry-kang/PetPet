namespace PetPet.Application.Media;

public interface IMediaService
{
    Task<MediaUploadResult> UploadImageAsync(Stream stream, string claimedContentType, long length, string ownerEmail);
    Task<MediaFileResult?> GetMediaFileAsync(Guid id, string? viewerEmail);
    Task<int> CleanupOrphansAsync(TimeSpan olderThan);
    Task<string?> ValidateAndResolveImageAsync(string? imageUrl, string userEmail);
}
