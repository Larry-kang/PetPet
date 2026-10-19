using System.Collections.Concurrent;
using PetPet.Application.Chat;

namespace PetPet.Infrastructure.Services;

public class ChatRateLimiter : IChatRateLimiter
{
    private sealed class MemberBucket
    {
        public readonly object SyncRoot = new();
        public DateTime WindowStart = DateTime.UtcNow;
        public int Count = 0;
    }

    private readonly ConcurrentDictionary<string, MemberBucket> _buckets = new(StringComparer.OrdinalIgnoreCase);

    public ChatRateLimitResult TryAcquire(string memberEmail, int maxPerMinute = 60)
    {
        if (string.IsNullOrWhiteSpace(memberEmail))
        {
            return new ChatRateLimitResult(false, 60, 0);
        }

        var normalizedKey = memberEmail.Trim().ToUpperInvariant();
        var bucket = _buckets.GetOrAdd(normalizedKey, _ => new MemberBucket());

        lock (bucket.SyncRoot)
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - bucket.WindowStart).TotalSeconds;

            if (elapsed >= 60.0)
            {
                bucket.WindowStart = now;
                bucket.Count = 0;
                elapsed = 0;
            }

            if (bucket.Count < maxPerMinute)
            {
                bucket.Count++;
                return new ChatRateLimitResult(true, 0, bucket.Count);
            }

            var remaining = Math.Max(1, (int)Math.Ceiling(60.0 - elapsed));
            return new ChatRateLimitResult(false, remaining, bucket.Count);
        }
    }

    public void Reset()
    {
        _buckets.Clear();
    }
}
