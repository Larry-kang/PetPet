using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPet.Application.Members;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;
using PetPet.Infrastructure.Demo;
using PetPet.Infrastructure.Services;

namespace PetPet.UnitTests;

public class DemoDatabaseAndSeedIntegrationTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    private string CreateTempDbPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"petpet_test_{Guid.NewGuid():N}.db");
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file)) File.Delete(file);
                var shm = file + "-shm";
                var wal = file + "-wal";
                if (File.Exists(shm)) File.Delete(shm);
                if (File.Exists(wal)) File.Delete(wal);
            }
            catch {}
        }
    }

    private static IServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IMemberPasswordHasher, MemberPasswordHasher>();
        services.AddScoped<IMemberRepository, MemberRepository>();
        return services.BuildServiceProvider();
    }

    [Fact]
    [Trait("Requirement", "PP-SEED-01")]
    [Trait("Requirement", "PP-SEED-02")]
    public void DeterministicGenerator_CreatesExactCountsAndGroupings()
    {
        var data = DemoDataGenerator.Generate(20261013, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));

        // Members
        Assert.Equal(120, data.Members.Count);
        Assert.Single(data.Members, m => m.IsAdmin);
        Assert.Single(data.Members, m => !m.IsEnabled);
        Assert.Equal(118, data.Members.Count(m => m.IsEnabled && !m.IsAdmin));

        // Fixed personas
        Assert.Contains(data.Members, m => m.Email == "admin@example.test" && m.IsAdmin);
        Assert.Contains(data.Members, m => m.Email == "disabled@example.test" && !m.IsEnabled);
        Assert.Contains(data.Members, m => m.Email == "demo-owner@example.test" && m.IsMatchEnabled);
        Assert.Contains(data.Members, m => m.Email == "demo-nopet@example.test");
        Assert.Contains(data.Members, m => m.Email == "demo-friend@example.test");

        // Pets
        Assert.Equal(240, data.Pets.Count);
        var petsByOwner = data.Pets.GroupBy(p => p.OwnerEmail).ToDictionary(g => g.Key, g => g.Count());

        // Count distribution: exactly 24 members with 0, 1, 2, 3, 4 pets
        var counts = new int[5];
        foreach (var m in data.Members)
        {
            int pCount = petsByOwner.TryGetValue(m.Email, out int val) ? val : 0;
            Assert.InRange(pCount, 0, 4);
            counts[pCount]++;
        }
        for (int i = 0; i <= 4; i++)
        {
            Assert.Equal(24, counts[i]);
        }

        // Demo owner has 4 pets, demo nopet has 0 pets
        Assert.Equal(4, petsByOwner["demo-owner@example.test"]);
        Assert.False(petsByOwner.ContainsKey("demo-nopet@example.test"));

        // Posts
        Assert.Equal(1200, data.Posts.Count);
        Assert.Equal(1180, data.Posts.Count(p => p.IsEnabled));
        Assert.Equal(20, data.Posts.Count(p => !p.IsEnabled));

        // Every eligible non-admin enabled member has >= 1 post
        var authors = data.Posts.Select(p => p.AuthorEmail).Distinct().ToHashSet();
        Assert.Equal(118, authors.Count);

        // Comments
        Assert.Equal(2400, data.Comments.Count);

        // Friends
        Assert.Equal(300, data.Friends.Count);
        Assert.Equal(180, data.Friends.Count(f => f.IsAccepted));
        Assert.Equal(120, data.Friends.Count(f => !f.IsAccepted));

        // Unique pairs in friends
        var friendPairs = data.Friends.Select(f => $"{f.MemberAEmail}:{f.MemberBEmail}").ToHashSet();
        Assert.Equal(300, friendPairs.Count);

        // Demo owner has outgoing pending to demo-friend and incoming pending
        Assert.Contains(data.Friends, f => f.RequesterEmail == "demo-owner@example.test" && f.AddresseeEmail == "demo-friend@example.test" && !f.IsAccepted);
        Assert.Contains(data.Friends, f => f.AddresseeEmail == "demo-owner@example.test" && !f.IsAccepted);
        Assert.Contains(data.Friends, f => (f.RequesterEmail == "demo-owner@example.test" || f.AddresseeEmail == "demo-owner@example.test") && f.IsAccepted);
    }

    [Fact]
    [Trait("Requirement", "PP-SEED-01")]
    public void DeterministicGenerator_IsStrictlyReproducible_AndRejectsFutureBaseTime()
    {
        var run1 = DemoDataGenerator.Generate(20261013, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        var run2 = DemoDataGenerator.Generate(20261013, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(run1.BusinessFingerprint, run2.BusinessFingerprint);
        Assert.NotEmpty(run1.BusinessFingerprint);

        Assert.Throws<ArgumentException>(() =>
            DemoDataGenerator.Generate(20261013, DateTime.UtcNow.AddDays(1)));
    }

    [Fact]
    [Trait("Requirement", "PP-DB-01")]
    [Trait("Requirement", "PP-DB-02")]
    [Trait("Requirement", "PP-DB-03")]
    [Trait("Requirement", "PP-SEED-04")]
    public async Task RealSqlite_MigrationAndSeed_IsIdempotent_AndRetainsUserModifications()
    {
        var dbPath = CreateTempDbPath();
        var options = new DbContextOptionsBuilder<PetPetDbContext>()
            .UseSqlite($"Data Source={dbPath}", b => b.MigrationsAssembly("PetPet.Migrations.Sqlite"))
            .Options;

        var services = CreateServices();

        // 1. First run: Migrate + Seed
        using (var db = new PetPetDbContext(options))
        {
            await db.Database.MigrateAsync();
            var result = await DemoSeeder.SeedAsync(db, services, 20261013, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.False(result.AlreadySeeded);
            Assert.NotNull(result.Record);
            Assert.Equal(120, await db.Members.CountAsync());
            Assert.Equal(240, await db.Pets.CountAsync());
            Assert.Equal(1200, await db.Posts.CountAsync());
            Assert.Equal(2400, await db.Comments.CountAsync());
            Assert.Equal(300, await db.Friends.CountAsync());
            Assert.True(await db.Likes.AnyAsync());
        }

        // 2. User modification simulation
        using (var db = new PetPetDbContext(options))
        {
            var user = await db.Members.FirstAsync(m => m.Email == "demo-owner@example.test");
            user.Name = "User Modified Name";
            await db.SaveChangesAsync();

            // Add a new user post
            db.Posts.Add(new Post
            {
                AuthorEmail = user.Email,
                Title = "User Created New Post",
                Content = "Brand new post content",
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true
            });
            await db.SaveChangesAsync();
            Assert.Equal(1201, await db.Posts.CountAsync());
        }

        // 3. Second run (restart simulation): Should detect SeedRecord, NOT re-seed, NOT wipe user edits
        using (var db = new PetPetDbContext(options))
        {
            var result = await DemoSeeder.SeedAsync(db, services, 20261013, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.True(result.AlreadySeeded);
            Assert.Equal(120, await db.Members.CountAsync());
            Assert.Equal(1201, await db.Posts.CountAsync()); // User post is retained!

            var user = await db.Members.FirstAsync(m => m.Email == "demo-owner@example.test");
            Assert.Equal("User Modified Name", user.Name); // User modification is preserved!
        }
    }

    [Fact]
    [Trait("Requirement", "PP-DB-03")]
    public async Task RealSqlite_UnmarkedNonEmptyDatabase_RefusesSeed()
    {
        var dbPath = CreateTempDbPath();
        var options = new DbContextOptionsBuilder<PetPetDbContext>()
            .UseSqlite($"Data Source={dbPath}", b => b.MigrationsAssembly("PetPet.Migrations.Sqlite"))
            .Options;

        var services = CreateServices();

        using (var db = new PetPetDbContext(options))
        {
            await db.Database.MigrateAsync();
            // Insert a manual member without SeedRecord
            db.Members.Add(new Member
            {
                Email = "manual@example.com",
                NormalizedEmail = "MANUAL@EXAMPLE.COM",
                Password = "hash",
                Name = "Manual",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                Version = Guid.NewGuid()
            });
            await db.SaveChangesAsync();

            // Attempting to seed unmarked database should throw
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                DemoSeeder.SeedAsync(db, services, 20261013, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)));

            Assert.Contains("非展示初始資料", ex.Message);
        }
    }

    [Fact]
    [Trait("Requirement", "PP-RUN-03")]
    public void DemoLockManager_ExclusiveRunLock_WorksAsExpected()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"petpet_lock_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Acquire lock
            using var lock1 = DemoLockManager.AcquireRunLock(tempDir);
            Assert.NotNull(lock1);

            // Second acquisition on same directory should fail
            Assert.Throws<InvalidOperationException>(() =>
                DemoLockManager.AcquireRunLock(tempDir));

            // Release lock
            DemoLockManager.ReleaseRunLock();

            // Subsequent acquisition succeeds
            using var lock2 = DemoLockManager.AcquireRunLock(tempDir);
            Assert.NotNull(lock2);
            DemoLockManager.ReleaseRunLock();
        }
        finally
        {
            DemoLockManager.ReleaseRunLock();
            try { Directory.Delete(tempDir, recursive: true); } catch {}
        }
    }
}
