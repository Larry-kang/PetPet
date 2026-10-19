namespace PetPet.Application.Chat;

public record ChatRateLimitResult(bool IsAllowed, int RetryAfterSeconds, int CurrentCount);

public interface IChatRateLimiter
{
    ChatRateLimitResult TryAcquire(string memberEmail, int maxPerMinute = 60);
    void Reset();
}
