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

public class FriendAndSocialIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _sqliteConnection;
    private readonly WebApplicationFactory<Program> _factory;

    public FriendAndSocialIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"petpet_soc_{Guid.NewGuid():N}.db");
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
        SeedInitialUsers(db, scope.ServiceProvider.GetRequiredService<IMemberPasswordHasher>());
    }

    private static void SeedInitialUsers(PetPetDbContext db, IMemberPasswordHasher hasher)
    {
        var alice = new Member
        {
            Email = "alice@petpet.test",
            NormalizedEmail = "ALICE@PETPET.TEST",
            Name = "Alice",
            Password = string.Empty,
            IsEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Version = Guid.NewGuid()
        };
        alice.Password = hasher.Hash(alice, "AlicePass123!");

        var bob = new Member
        {
            Email = "bob@petpet.test",
            NormalizedEmail = "BOB@PETPET.TEST",
            Name = "Bob",
            Password = string.Empty,
            IsEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Version = Guid.NewGuid()
        };
        bob.Password = hasher.Hash(bob, "BobPass123!");

        db.Members.AddRange(alice, bob);
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
    [Trait("Requirement", "PP-FRD-01")]
    public async Task Friend_SendAcceptRemove_Lifecycle_Verified()
    {
        var aliceClient = await CreateAuthenticatedClientAsync("alice@petpet.test", "AlicePass123!");
        var bobClient = await CreateAuthenticatedClientAsync("bob@petpet.test", "BobPass123!");

        // Alice sends friend request to Bob
        var aliceToken = await ExtractAntiforgeryToken(aliceClient, "/Friend/Index");
        var sendResp = await aliceClient.PostAsync("/Friend/SendRequest", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["targetEmail"] = "bob@petpet.test",
            ["__RequestVerificationToken"] = aliceToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, sendResp.StatusCode);

        // Verify request exists in DB
        int friendId;
        Guid friendVersion;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var req = await db.Friends.FirstOrDefaultAsync(f => f.RequesterEmail == "alice@petpet.test" && f.AddresseeEmail == "bob@petpet.test");
            Assert.NotNull(req);
            Assert.False(req.IsAccepted);
            friendId = req.Id;
            friendVersion = req.Version;
        }

        // Bob accepts friend request
        var bobToken = await ExtractAntiforgeryToken(bobClient, "/Friend/Index?tab=received");
        var acceptResp = await bobClient.PostAsync("/Friend/Accept", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = friendId.ToString(),
            ["version"] = friendVersion.ToString(),
            ["__RequestVerificationToken"] = bobToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, acceptResp.StatusCode);

        // Verify in DB that it is accepted
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var friend = await db.Friends.FindAsync(friendId);
            Assert.NotNull(friend);
            Assert.True(friend.IsAccepted);
            Assert.NotNull(friend.AcceptedAtUtc);
            friendVersion = friend.Version;
        }

        // Alice removes friendship
        var removeResp = await aliceClient.PostAsync("/Friend/Remove", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = friendId.ToString(),
            ["version"] = friendVersion.ToString(),
            ["__RequestVerificationToken"] = aliceToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, removeResp.StatusCode);

        // Verify removed from DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var friend = await db.Friends.FindAsync(friendId);
            Assert.Null(friend);
        }
    }

    [Fact]
    [Trait("Requirement", "PP-FRD-02")]
    public async Task Friend_ConcurrentReverseRequest_ReturnsConflict()
    {
        var aliceClient = await CreateAuthenticatedClientAsync("alice@petpet.test", "AlicePass123!");
        var bobClient = await CreateAuthenticatedClientAsync("bob@petpet.test", "BobPass123!");

        // Alice sends request to Bob
        var aliceToken = await ExtractAntiforgeryToken(aliceClient, "/Friend/Index");
        var sendResp1 = await aliceClient.PostAsync("/Friend/SendRequest", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["targetEmail"] = "bob@petpet.test",
            ["__RequestVerificationToken"] = aliceToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, sendResp1.StatusCode);

        // Bob simultaneously sends request to Alice without accepting
        var bobToken = await ExtractAntiforgeryToken(bobClient, "/Friend/Index");
        var sendResp2 = await bobClient.PostAsync("/Friend/SendRequest", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["targetEmail"] = "alice@petpet.test",
            ["__RequestVerificationToken"] = bobToken
        }));

        // Expect 409 Conflict
        Assert.Equal(HttpStatusCode.Conflict, sendResp2.StatusCode);
    }

    [Fact]
    [Trait("Requirement", "PP-FRD-01")]
    public async Task Friend_SelfRequest_ReturnsBadRequest()
    {
        var aliceClient = await CreateAuthenticatedClientAsync("alice@petpet.test", "AlicePass123!");
        var aliceToken = await ExtractAntiforgeryToken(aliceClient, "/Friend/Index");

        var sendResp = await aliceClient.PostAsync("/Friend/SendRequest", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["targetEmail"] = "alice@petpet.test",
            ["__RequestVerificationToken"] = aliceToken
        }));

        Assert.Equal(HttpStatusCode.BadRequest, sendResp.StatusCode);
    }

    [Fact]
    [Trait("Requirement", "PP-SOC-04")]
    public async Task SetLike_DesiredState_IsIdempotent()
    {
        var aliceClient = await CreateAuthenticatedClientAsync("alice@petpet.test", "AlicePass123!");

        int postId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var post = new Post
            {
                AuthorEmail = "bob@petpet.test",
                Title = "Bob's Post",
                Content = "Hello from Bob",
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true,
                IsDeleted = false
            };
            db.Posts.Add(post);
            await db.SaveChangesAsync();
            postId = post.Id;
        }

        var token = await ExtractAntiforgeryToken(aliceClient, $"/Post/Details/{postId}");

        // Call 1: desiredLiked = true
        var likeResp1 = await aliceClient.PostAsync("/Post/SetLike", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["postId"] = postId.ToString(),
            ["desiredLiked"] = "true",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, likeResp1.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var likeCount = await db.Likes.CountAsync(l => l.PostId == postId && l.UserEmail == "alice@petpet.test");
            Assert.Equal(1, likeCount);
        }

        // Call 2: desiredLiked = true again (idempotent)
        var likeResp2 = await aliceClient.PostAsync("/Post/SetLike", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["postId"] = postId.ToString(),
            ["desiredLiked"] = "true",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, likeResp2.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var likeCount = await db.Likes.CountAsync(l => l.PostId == postId && l.UserEmail == "alice@petpet.test");
            Assert.Equal(1, likeCount);
        }

        // Call 3: desiredLiked = false
        var unlikeResp1 = await aliceClient.PostAsync("/Post/SetLike", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["postId"] = postId.ToString(),
            ["desiredLiked"] = "false",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, unlikeResp1.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var likeCount = await db.Likes.CountAsync(l => l.PostId == postId && l.UserEmail == "alice@petpet.test");
            Assert.Equal(0, likeCount);
        }
    }

    [Fact]
    [Trait("Requirement", "PP-DATA-03")]
    public async Task Post_SoftDelete_PreventsAccessAndNonAuthorForbidden()
    {
        var aliceClient = await CreateAuthenticatedClientAsync("alice@petpet.test", "AlicePass123!");
        var bobClient = await CreateAuthenticatedClientAsync("bob@petpet.test", "BobPass123!");

        int postId;
        Guid postVersion;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var post = new Post
            {
                AuthorEmail = "alice@petpet.test",
                Title = "Alice's Secret Post",
                Content = "Post content here",
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true,
                IsDeleted = false,
                Version = Guid.NewGuid()
            };
            db.Posts.Add(post);
            await db.SaveChangesAsync();
            postId = post.Id;
            postVersion = post.Version;
        }

        // Bob attempts to delete Alice's post
        var bobToken = await ExtractAntiforgeryToken(bobClient, $"/Post/Details/{postId}");
        var bobDeleteResp = await bobClient.PostAsync($"/Post/Delete/{postId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["version"] = postVersion.ToString(),
            ["__RequestVerificationToken"] = bobToken
        }));
        Assert.True(
            bobDeleteResp.StatusCode == HttpStatusCode.Forbidden ||
            (bobDeleteResp.StatusCode == HttpStatusCode.Redirect && bobDeleteResp.Headers.Location?.OriginalString.Contains("AccessDenied") == true),
            "Expected Forbidden or Redirect to AccessDenied");

        // Alice deletes her own post
        var aliceToken = await ExtractAntiforgeryToken(aliceClient, $"/Post/Details/{postId}");
        var aliceDeleteResp = await aliceClient.PostAsync($"/Post/Delete/{postId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["version"] = postVersion.ToString(),
            ["__RequestVerificationToken"] = aliceToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, aliceDeleteResp.StatusCode);

        // Details now returns 404 NotFound
        var getDetailsResp = await aliceClient.GetAsync($"/Post/Details/{postId}");
        Assert.Equal(HttpStatusCode.NotFound, getDetailsResp.StatusCode);
    }
}
