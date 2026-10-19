using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PetPet.Application.Members;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;
using PetPet.Infrastructure.Services;

namespace PetPet.UnitTests;

public class BlockAndModerationIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _sqliteConnection;
    private readonly WebApplicationFactory<Program> _factory;

    public BlockAndModerationIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"petpet_mod_{Guid.NewGuid():N}.db");
        _sqliteConnection = new SqliteConnection($"Data Source={_dbPath}");
        _sqliteConnection.Open();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<PetPetDbContext>>();
                services.AddDbContext<PetPetDbContext>(options =>
                {
                    options.UseSqlite(_sqliteConnection, b => b.MigrationsAssembly("PetPet.Migrations.Sqlite"));
                });
            });
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
        db.Database.EnsureCreated();
        SeedInitialData(db, scope.ServiceProvider.GetRequiredService<IMemberPasswordHasher>());
    }

    private static void SeedInitialData(PetPetDbContext db, IMemberPasswordHasher hasher)
    {
        var admin = new Member
        {
            Email = "admin@mod.test",
            NormalizedEmail = "ADMIN@MOD.TEST",
            PublicId = Guid.NewGuid(),
            Name = "管理員",
            Password = string.Empty,
            IsAdmin = true,
            IsEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Version = Guid.NewGuid()
        };
        admin.Password = hasher.Hash(admin, "AdminPass123!");

        var alice = new Member
        {
            Email = "alice@mod.test",
            NormalizedEmail = "ALICE@MOD.TEST",
            PublicId = Guid.NewGuid(),
            Name = "Alice",
            Password = string.Empty,
            IsEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Version = Guid.NewGuid()
        };
        alice.Password = hasher.Hash(alice, "AlicePass123!");

        var bob = new Member
        {
            Email = "bob@mod.test",
            NormalizedEmail = "BOB@MOD.TEST",
            PublicId = Guid.NewGuid(),
            Name = "Bob",
            Password = string.Empty,
            IsEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Version = Guid.NewGuid()
        };
        bob.Password = hasher.Hash(bob, "BobPass123!");

        db.Members.AddRange(admin, alice, bob);

        // Pre-establish friendship between Alice and Bob
        var friend = new Friend
        {
            RequesterEmail = "alice@mod.test",
            AddresseeEmail = "bob@mod.test",
            MemberAEmail = "alice@mod.test",
            MemberBEmail = "bob@mod.test",
            IsAccepted = true,
            CreatedAt = DateTime.UtcNow,
            AcceptedAtUtc = DateTime.UtcNow,
            Version = Guid.NewGuid()
        };
        db.Friends.Add(friend);

        // Bob creates a post
        var post = new Post
        {
            AuthorEmail = "bob@mod.test",
            Title = "Bob's Spammy Post",
            Content = "Buy cheap meds now!",
            CreatedAt = DateTime.UtcNow,
            IsEnabled = true,
            IsDeleted = false,
            Version = Guid.NewGuid()
        };
        db.Posts.Add(post);

        db.SaveChanges();
    }

    public void Dispose()
    {
        _factory.Dispose();
        _sqliteConnection.Dispose();
        try
        {
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
            var shm = _dbPath + "-shm";
            var wal = _dbPath + "-wal";
            if (File.Exists(shm)) File.Delete(shm);
            if (File.Exists(wal)) File.Delete(wal);
        }
        catch {}
    }

    private static async Task<string> ExtractAntiforgeryToken(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var token = await ExtractAntiforgeryToken(client, "/Member/Login");
        var response = await client.PostAsync("/Member/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password,
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    [Fact]
    [Trait("Requirement", "PP-BLK-01")]
    public async Task Block_SeversFriendship_AndHidesProfile_Mutual()
    {
        var aliceClient = await CreateAuthenticatedClientAsync("alice@mod.test", "AlicePass123!");
        var bobClient = await CreateAuthenticatedClientAsync("bob@mod.test", "BobPass123!");

        Guid alicePublicId;
        Guid bobPublicId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var alice = await db.Members.FirstAsync(m => m.Email == "alice@mod.test");
            var bob = await db.Members.FirstAsync(m => m.Email == "bob@mod.test");
            alicePublicId = alice.PublicId;
            bobPublicId = bob.PublicId;

            // Initially, friend relationship exists
            Assert.True(await db.Friends.AnyAsync(f => f.MemberAEmail == "alice@mod.test" && f.MemberBEmail == "bob@mod.test" && f.IsAccepted));
        }

        // Alice blocks Bob
        var aliceToken = await ExtractAntiforgeryToken(aliceClient, "/Block/Index");
        var blockResp = await aliceClient.PostAsync("/Block/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["targetEmail"] = "bob@mod.test",
            ["__RequestVerificationToken"] = aliceToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, blockResp.StatusCode);

        // Verify friend relationship is severed in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            Assert.False(await db.Friends.AnyAsync(f => f.MemberAEmail == "alice@mod.test" && f.MemberBEmail == "bob@mod.test"));
            Assert.True(await db.Blocks.AnyAsync(b => b.BlockerEmail == "alice@mod.test" && b.BlockedEmail == "bob@mod.test"));
        }

        // Bob viewing Alice's profile returns 404
        var bobViewAlice = await bobClient.GetAsync($"/Members/{alicePublicId}");
        Assert.Equal(HttpStatusCode.NotFound, bobViewAlice.StatusCode);

        // Alice viewing Bob's profile returns 404
        var aliceViewBob = await aliceClient.GetAsync($"/Members/{bobPublicId}");
        Assert.Equal(HttpStatusCode.NotFound, aliceViewBob.StatusCode);

        // Alice unblocks Bob
        var unblockToken = await ExtractAntiforgeryToken(aliceClient, "/Block/Index");
        var unblockResp = await aliceClient.PostAsync("/Block/Remove", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["targetEmail"] = "bob@mod.test",
            ["__RequestVerificationToken"] = unblockToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, unblockResp.StatusCode);

        // Block is removed, but friend relationship is NOT restored
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            Assert.False(await db.Blocks.AnyAsync(b => b.BlockerEmail == "alice@mod.test" && b.BlockedEmail == "bob@mod.test"));
            Assert.False(await db.Friends.AnyAsync(f => f.MemberAEmail == "alice@mod.test" && f.MemberBEmail == "bob@mod.test"));
        }
    }

    [Fact]
    [Trait("Requirement", "PP-MOD-01")]
    public async Task Report_DuplicatePending_Prevented()
    {
        var aliceClient = await CreateAuthenticatedClientAsync("alice@mod.test", "AlicePass123!");

        int postId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var post = await db.Posts.FirstAsync(p => p.Title == "Bob's Spammy Post");
            postId = post.Id;
        }

        var token = await ExtractAntiforgeryToken(aliceClient, $"/Report/Create?postId={postId}");

        // First report
        var reportResp1 = await aliceClient.PostAsync("/Report/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["PostId"] = postId.ToString(),
            ["Category"] = "Spam",
            ["Reason"] = "廣告貼文",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, reportResp1.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var count = await db.Reports.CountAsync(r => r.PostId == postId && r.ReporterEmail == "alice@mod.test" && r.Status == "Pending");
            Assert.Equal(1, count);
        }

        // Second report on same post while status is Pending
        var token2 = await ExtractAntiforgeryToken(aliceClient, $"/Report/Create?postId={postId}");
        var reportResp2 = await aliceClient.PostAsync("/Report/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["PostId"] = postId.ToString(),
            ["Category"] = "Spam",
            ["Reason"] = "再次檢舉",
            ["__RequestVerificationToken"] = token2
        }));
        Assert.Equal(HttpStatusCode.Redirect, reportResp2.StatusCode);

        // Count is still exactly 1
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var count = await db.Reports.CountAsync(r => r.PostId == postId && r.ReporterEmail == "alice@mod.test" && r.Status == "Pending");
            Assert.Equal(1, count);
        }
    }

    [Fact]
    [Trait("Requirement", "PP-MOD-02")]
    public async Task Admin_ResolveReport_WithHidePost_AndRestore()
    {
        var adminClient = await CreateAuthenticatedClientAsync("admin@mod.test", "AdminPass123!");

        int reportId;
        Guid reportVersion;
        int postId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var post = await db.Posts.FirstAsync(p => p.Title == "Bob's Spammy Post");
            postId = post.Id;

            var report = new Report
            {
                PostId = postId,
                ReporterEmail = "alice@mod.test",
                Category = "Spam",
                Reason = "Spam content",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                Version = Guid.NewGuid()
            };
            db.Reports.Add(report);
            await db.SaveChangesAsync();
            reportId = report.Id;
            reportVersion = report.Version;
        }

        var adminToken = await ExtractAntiforgeryToken(adminClient, "/Admin/ManageReports");

        // Admin resolves report and hides post
        var resolveResp = await adminClient.PostAsync("/Admin/ResolveReport", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = reportId.ToString(),
            ["version"] = reportVersion.ToString(),
            ["hidePost"] = "true",
            ["note"] = "確認違規，隱藏貼文",
            ["__RequestVerificationToken"] = adminToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, resolveResp.StatusCode);

        // Post is now disabled (hidden)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var post = await db.Posts.FindAsync(postId);
            Assert.NotNull(post);
            Assert.False(post.IsEnabled);

            var report = await db.Reports.FindAsync(reportId);
            Assert.NotNull(report);
            Assert.Equal("Resolved", report.Status);
        }

        // Admin restores post
        var restoreResp = await adminClient.PostAsync("/Admin/RestorePost", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["postId"] = postId.ToString(),
            ["__RequestVerificationToken"] = adminToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, restoreResp.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var post = await db.Posts.FindAsync(postId);
            Assert.NotNull(post);
            Assert.True(post.IsEnabled);
        }
    }
}
