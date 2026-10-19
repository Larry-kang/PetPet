using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PetPet.Application.Members;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;

namespace PetPet.Infrastructure.Demo;

public class SeedResult
{
    public bool AlreadySeeded { get; set; }
    public SeedRecord? Record { get; set; }
    public Dictionary<string, int> CurrentCounts { get; set; } = [];
}

public static class DemoSeeder
{
    public const string FixedDemoPassword = "DemoPass!2026";

    public static async Task<SeedResult> SeedAsync(
        PetPetDbContext context,
        IServiceProvider services,
        long seed = 20261013,
        DateTime? baseTime = null,
        int generatorVersion = 1)
    {
        var logger = services.GetService<ILoggerFactory>()?.CreateLogger("DemoSeeder");
        var baseTimeUtc = baseTime ?? new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        bool hasSeedRecord = await context.SeedRecords.AnyAsync();
        bool hasMembers = await context.Members.AnyAsync();

        if (hasSeedRecord && hasMembers)
        {
            logger?.LogInformation("展示資料庫已完成初始化 (SeedRecord 存在且非空)，保留現有資料。");
            var record = await context.SeedRecords.OrderByDescending(r => r.Id).FirstOrDefaultAsync();
            return new SeedResult
            {
                AlreadySeeded = true,
                Record = record,
                CurrentCounts = await GetCurrentCountsAsync(context)
            };
        }

        if (!hasSeedRecord && hasMembers)
        {
            throw new InvalidOperationException("資料庫已有會員資料但無 SeedRecord 標記。此可能為非展示初始資料，請選擇獨立展示資料庫。");
        }

        logger?.LogInformation("開始生成與寫入確定性展示資料 (Seed={Seed}, BaseTime={BaseTime})...", seed, baseTimeUtc);

        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var data = DemoDataGenerator.Generate(seed, baseTimeUtc, generatorVersion);

            // 1. Catalogs
            if (!await context.Cities.AnyAsync())
            {
                context.Cities.AddRange(data.Cities);
                await context.SaveChangesAsync();
            }
            if (!await context.Varieties.AnyAsync())
            {
                context.Varieties.AddRange(data.Varieties);
                await context.SaveChangesAsync();
            }

            // 2. Members with Hashed Passwords
            var hasher = services.GetRequiredService<IMemberPasswordHasher>();
            foreach (var member in data.Members)
            {
                member.Password = hasher.Hash(member, FixedDemoPassword);
            }
            context.Members.AddRange(data.Members);
            await context.SaveChangesAsync();

            // 3. Pets
            context.Pets.AddRange(data.Pets);
            await context.SaveChangesAsync();

            // 4. Posts
            context.Posts.AddRange(data.Posts);
            await context.SaveChangesAsync();

            // 5. Comments
            context.Comments.AddRange(data.Comments);
            await context.SaveChangesAsync();

            // 6. Likes
            context.Likes.AddRange(data.Likes);
            await context.SaveChangesAsync();

            // 7. Friends
            context.Friends.AddRange(data.Friends);
            await context.SaveChangesAsync();

            // 8. Integrity Verifications
            int memberCount = await context.Members.CountAsync();
            int petCount = await context.Pets.CountAsync();
            int postCount = await context.Posts.CountAsync();
            int commentCount = await context.Comments.CountAsync();
            int friendCount = await context.Friends.CountAsync();
            int likeCount = await context.Likes.CountAsync();
            int acceptedFriends = await context.Friends.CountAsync(f => f.IsAccepted);
            int pendingFriends = await context.Friends.CountAsync(f => !f.IsAccepted);
            int visiblePosts = await context.Posts.CountAsync(p => p.IsEnabled);
            int hiddenPosts = await context.Posts.CountAsync(p => !p.IsEnabled);

            if (memberCount != 120 || petCount != 240 || postCount != 1200 || commentCount != 2400 || friendCount != 300)
            {
                throw new InvalidOperationException($"資料筆數驗證不符: Members={memberCount}, Pets={petCount}, Posts={postCount}, Comments={commentCount}, Friends={friendCount}");
            }

            // Verify password hash for demo-owner
            var testOwner = await context.Members.FirstAsync(m => m.Email == "demo-owner@example.test");
            var check = hasher.Verify(testOwner, FixedDemoPassword);
            if (check == PasswordCheck.Failed)
            {
                throw new InvalidOperationException("合成展示帳號密碼雜湊驗證失敗。");
            }

            // 9. Write SeedRecord
            var countsDict = new Dictionary<string, int>
            {
                ["Members"] = memberCount,
                ["Pets"] = petCount,
                ["Posts"] = postCount,
                ["VisiblePosts"] = visiblePosts,
                ["HiddenPosts"] = hiddenPosts,
                ["Comments"] = commentCount,
                ["Friends"] = friendCount,
                ["AcceptedFriends"] = acceptedFriends,
                ["PendingFriends"] = pendingFriends,
                ["Likes"] = likeCount
            };

            var seedRecord = new SeedRecord
            {
                GeneratorVersion = generatorVersion,
                Seed = seed,
                BaseTimeUtc = baseTimeUtc,
                CompletedAtUtc = DateTime.UtcNow,
                CountsJson = JsonSerializer.Serialize(countsDict),
                BusinessFingerprint = data.BusinessFingerprint
            };

            context.SeedRecords.Add(seedRecord);
            await context.SaveChangesAsync();

            await transaction.CommitAsync();
            logger?.LogInformation("展示資料寫入成功！Members={Members}, Pets={Pets}, Posts={Posts}, Comments={Comments}, Friends={Friends}, Likes={Likes}",
                memberCount, petCount, postCount, commentCount, friendCount, likeCount);

            return new SeedResult
            {
                AlreadySeeded = false,
                Record = seedRecord,
                CurrentCounts = countsDict
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger?.LogError(ex, "展示資料初始化失敗，已完全復原交易。");
            throw;
        }
    }

    public static async Task<Dictionary<string, int>> GetCurrentCountsAsync(PetPetDbContext context)
    {
        return new Dictionary<string, int>
        {
            ["Members"] = await context.Members.CountAsync(),
            ["Pets"] = await context.Pets.CountAsync(),
            ["Posts"] = await context.Posts.CountAsync(),
            ["VisiblePosts"] = await context.Posts.CountAsync(p => p.IsEnabled && !p.IsDeleted),
            ["Comments"] = await context.Comments.CountAsync(c => !c.IsDeleted),
            ["Friends"] = await context.Friends.CountAsync(),
            ["AcceptedFriends"] = await context.Friends.CountAsync(f => f.IsAccepted),
            ["PendingFriends"] = await context.Friends.CountAsync(f => !f.IsAccepted),
            ["Likes"] = await context.Likes.CountAsync()
        };
    }
}
