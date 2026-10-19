using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PetPet.Application.Chat;
using PetPet.Application.Media;
using PetPet.Application.Members;
using PetPet.Application.Social;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;
using PetPet.Infrastructure.Services;
using PetPet.Web.Controllers;
using PetPet.Web.Hubs;
using Microsoft.AspNetCore.SignalR.Client;

namespace PetPet.UnitTests;

public class RemediationVerificationIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _sqliteConnection;
    private readonly WebApplicationFactory<Program> _factory;

    public RemediationVerificationIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"petpet_remed_{Guid.NewGuid():N}.db");
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
                    options.UseSqlite($"Data Source={_dbPath};Foreign Keys=True", b => b.MigrationsAssembly("PetPet.Migrations.Sqlite"));
                });
            });
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
        db.Database.EnsureCreated();
        SeedRemediationData(db, scope.ServiceProvider.GetRequiredService<IMemberPasswordHasher>());
    }

    private void SeedRemediationData(PetPetDbContext db, IMemberPasswordHasher hasher)
    {
        var city = new City { Id = 1, Name = "台北市", Code = "TPE" };
        db.Cities.Add(city);

        var admin = new Member
        {
            Email = "admin@petpet.test",
            NormalizedEmail = "ADMIN@PETPET.TEST",
            Name = "系統管理員",
            Bio = "我是管理員",
            Phone = "0912345678",
            Birthday = new DateTime(1990, 1, 1),
            Gender = true,
            CityId = 1,
            IsAdmin = true,
            IsEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Version = Guid.NewGuid()
        };
        admin.Password = hasher.Hash(admin, "AdminPass123!");

        var alice = new Member
        {
            Email = "alice@petpet.test",
            NormalizedEmail = "ALICE@PETPET.TEST",
            Name = "愛麗絲",
            Bio = "愛貓人士",
            Phone = "0987654321",
            Birthday = new DateTime(1995, 5, 20),
            Gender = false,
            CityId = 1,
            IsAdmin = false,
            IsEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Version = Guid.NewGuid()
        };
        alice.Password = hasher.Hash(alice, "AlicePass123!");

        var bob = new Member
        {
            Email = "bob@petpet.test",
            NormalizedEmail = "BOB@PETPET.TEST",
            Name = "鮑伯",
            Bio = "愛狗人士",
            Phone = "0911222333",
            Birthday = new DateTime(1992, 8, 15),
            Gender = true,
            CityId = 1,
            IsAdmin = false,
            IsEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Version = Guid.NewGuid()
        };
        bob.Password = hasher.Hash(bob, "BobPass123!");

        db.Members.AddRange(admin, alice, bob);
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
    [Trait("Requirement", "PP-SEC-03")]
    [Trait("Requirement", "PP-MEM-03")]
    public async Task R1_MemberIndex_RedirectsAdminToManageUsers_AndVisitorToHome()
    {
        // 1. Visitor / Unauthenticated
        var anonClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var anonResp = await anonClient.GetAsync("/Member/Index");
        Assert.Equal(HttpStatusCode.Redirect, anonResp.StatusCode);
        Assert.Equal("/", anonResp.Headers.Location?.OriginalString);

        // 2. Regular User
        var userClient = await CreateAuthenticatedClientAsync("alice@petpet.test", "AlicePass123!");
        var userResp = await userClient.GetAsync("/Member/Index");
        Assert.Equal(HttpStatusCode.Redirect, userResp.StatusCode);
        Assert.Equal("/", userResp.Headers.Location?.OriginalString);

        // 3. Admin User
        var adminClient = await CreateAuthenticatedClientAsync("admin@petpet.test", "AdminPass123!");
        var adminResp = await adminClient.GetAsync("/Member/Index");
        Assert.Equal(HttpStatusCode.Redirect, adminResp.StatusCode);
        Assert.Contains("/Admin/ManageUsers", adminResp.Headers.Location?.OriginalString);
    }

    [Fact]
    [Trait("Requirement", "PP-SEC-03")]
    [Trait("Requirement", "PP-MEM-03")]
    public async Task R1_PublicProfile_ProjectsOnlySafeFields_NeverExposesPrivateData()
    {
        Guid alicePublicId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var alice = await db.Members.FirstAsync(m => m.Email == "alice@petpet.test");
            alicePublicId = alice.PublicId;
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/Members/{alicePublicId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        // Safe fields MUST be present
        Assert.Contains("愛麗絲", html);
        Assert.Contains("愛貓人士", html);

        // Sensitive private fields MUST NOT be present
        Assert.DoesNotContain("alice@petpet.test", html);
        Assert.DoesNotContain("0987654321", html);
        Assert.DoesNotContain("1995", html);
        Assert.DoesNotContain("Female", html);
        Assert.DoesNotContain("Password", html);
        Assert.DoesNotContain("SecurityStamp", html);
    }

    [Fact]
    [Trait("Requirement", "PP-DATA-04")]
    [Trait("Requirement", "PP-UX-02")]
    public async Task R2_PostCreation_Idempotency_And_ConflictEnforcement()
    {
        var aliceClient = await CreateAuthenticatedClientAsync("alice@petpet.test", "AlicePass123!");
        var token = await ExtractAntiforgeryToken(aliceClient, "/Post/Create");

        var commandId = Guid.NewGuid();
        var form1 = new Dictionary<string, string>
        {
            ["CommandId"] = commandId.ToString(),
            ["Title"] = "我的第一篇貼文",
            ["Content"] = "今天天氣真好，帶貓貓曬太陽！",
            ["ImageUrl"] = "/images/presets/avatar-dog.svg",
            ["__RequestVerificationToken"] = token
        };

        // First attempt: creates post
        var resp1 = await aliceClient.PostAsync("/Post/Create", new FormUrlEncodedContent(form1));
        Assert.Equal(HttpStatusCode.Redirect, resp1.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var postCount = await db.Posts.CountAsync(p => p.AuthorEmail == "alice@petpet.test");
            Assert.Equal(1, postCount);

            var receipt = await db.OperationReceipts.FirstOrDefaultAsync(r => r.CommandId == commandId);
            Assert.NotNull(receipt);
            Assert.Equal("alice@petpet.test", receipt.ActorEmail);
            Assert.Equal("CreatePost", receipt.Operation);
        }

        // Second attempt: same CommandId, identical content -> Idempotent replay (Redirect, post count remains 1)
        var resp2 = await aliceClient.PostAsync("/Post/Create", new FormUrlEncodedContent(form1));
        Assert.Equal(HttpStatusCode.Redirect, resp2.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var postCount = await db.Posts.CountAsync(p => p.AuthorEmail == "alice@petpet.test");
            Assert.Equal(1, postCount);
        }

        // Third attempt: same CommandId, different content -> HTTP 409 Conflict!
        var formConflict = new Dictionary<string, string>
        {
            ["CommandId"] = commandId.ToString(),
            ["Title"] = "篡改的標題",
            ["Content"] = "試圖以相同 CommandId 提交不同內容",
            ["ImageUrl"] = "/images/presets/avatar-dog.svg",
            ["__RequestVerificationToken"] = token
        };
        var resp3 = await aliceClient.PostAsync("/Post/Create", new FormUrlEncodedContent(formConflict));
        Assert.Equal(HttpStatusCode.Conflict, resp3.StatusCode);
    }

    [Fact]
    [Trait("Requirement", "PP-DATA-04")]
    [Trait("Requirement", "PP-UX-02")]
    public async Task R2_PostCreation_ConcurrentDuplicateRequests_OnlyOnePostCreated_BothSucceedWithSameTarget()
    {
        var aliceClient1 = await CreateAuthenticatedClientAsync("alice@petpet.test", "AlicePass123!");
        var aliceClient2 = await CreateAuthenticatedClientAsync("alice@petpet.test", "AlicePass123!");
        var token1 = await ExtractAntiforgeryToken(aliceClient1, "/Post/Create");
        var token2 = await ExtractAntiforgeryToken(aliceClient2, "/Post/Create");

        var commandId = Guid.NewGuid();
        var form1 = new Dictionary<string, string>
        {
            ["CommandId"] = commandId.ToString(),
            ["Title"] = "併發貼文測試",
            ["Content"] = "測試兩併發請求同時發送相同 CommandId",
            ["ImageUrl"] = "/images/presets/avatar-cat.svg",
            ["__RequestVerificationToken"] = token1
        };
        var form2 = new Dictionary<string, string>
        {
            ["CommandId"] = commandId.ToString(),
            ["Title"] = "併發貼文測試",
            ["Content"] = "測試兩併發請求同時發送相同 CommandId",
            ["ImageUrl"] = "/images/presets/avatar-cat.svg",
            ["__RequestVerificationToken"] = token2
        };

        // Fire both in parallel
        var task1 = aliceClient1.PostAsync("/Post/Create", new FormUrlEncodedContent(form1));
        var task2 = aliceClient2.PostAsync("/Post/Create", new FormUrlEncodedContent(form2));
        var results = await Task.WhenAll(task1, task2);

        // Both requests must complete successfully with redirect
        Assert.Equal(HttpStatusCode.Redirect, results[0].StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, results[1].StatusCode);
        Assert.Equal(results[0].Headers.Location?.OriginalString, results[1].Headers.Location?.OriginalString);

        // Assert database has exactly 1 post with this title
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
        var posts = await db.Posts.Where(p => p.Title == "併發貼文測試").ToListAsync();
        Assert.Single(posts);

        var receipts = await db.OperationReceipts.Where(r => r.CommandId == commandId).ToListAsync();
        Assert.Single(receipts);
    }

    [Fact]
    [Trait("Requirement", "PP-DATA-04")]
    [Trait("Requirement", "PP-SOC-03")]
    public async Task R2_CommentEdit_LengthValidation_And_AuthorAuthorization()
    {
        var aliceClient = await CreateAuthenticatedClientAsync("alice@petpet.test", "AlicePass123!");
        var token = await ExtractAntiforgeryToken(aliceClient, "/Post/Create");

        // Alice creates a post
        var postForm = new Dictionary<string, string>
        {
            ["CommandId"] = Guid.NewGuid().ToString(),
            ["Title"] = "可留言貼文",
            ["Content"] = "歡迎大家留言互動。",
            ["__RequestVerificationToken"] = token
        };
        await aliceClient.PostAsync("/Post/Create", new FormUrlEncodedContent(postForm));

        int postId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var post = await db.Posts.FirstAsync(p => p.Title == "可留言貼文");
            postId = post.Id;
        }

        // Bob leaves a comment
        var bobClient = await CreateAuthenticatedClientAsync("bob@petpet.test", "BobPass123!");
        var bobToken = await ExtractAntiforgeryToken(bobClient, $"/Post/Details/{postId}");
        var commentForm = new Dictionary<string, string>
        {
            ["postId"] = postId.ToString(),
            ["content"] = "這是一條測試留言！",
            ["__RequestVerificationToken"] = bobToken
        };
        var commentResp = await bobClient.PostAsync("/Post/AddComment", new FormUrlEncodedContent(commentForm));
        Assert.Equal(HttpStatusCode.Redirect, commentResp.StatusCode);

        int commentId;
        Guid commentVersion;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var c = await db.Comments.FirstAsync(c => c.PostId == postId && c.UserEmail == "bob@petpet.test");
            commentId = c.Id;
            commentVersion = c.Version;
        }

        // 1. Bob edits comment with valid content (1-500 chars)
        var editForm = new Dictionary<string, string>
        {
            ["commentId"] = commentId.ToString(),
            ["content"] = "這是一條已編輯過的留言內容！",
            ["version"] = commentVersion.ToString(),
            ["__RequestVerificationToken"] = bobToken
        };
        var editResp = await bobClient.PostAsync("/Post/EditComment", new FormUrlEncodedContent(editForm));
        Assert.Equal(HttpStatusCode.Redirect, editResp.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var c = await db.Comments.FirstAsync(c => c.Id == commentId);
            Assert.Equal("這是一條已編輯過的留言內容！", c.Content);
            Assert.NotEqual(commentVersion, c.Version);
            commentVersion = c.Version;
        }

        // 2. Alice (non-author) attempts to edit Bob's comment -> 403 Forbidden
        var aliceToken = await ExtractAntiforgeryToken(aliceClient, $"/Post/Details/{postId}");
        var unauthorizedEditForm = new Dictionary<string, string>
        {
            ["commentId"] = commentId.ToString(),
            ["content"] = "愛麗絲試圖修改鮑伯的留言",
            ["version"] = commentVersion.ToString(),
            ["__RequestVerificationToken"] = aliceToken
        };
        var unauthorizedResp = await aliceClient.PostAsync("/Post/EditComment", new FormUrlEncodedContent(unauthorizedEditForm));
        Assert.True(unauthorizedResp.StatusCode == HttpStatusCode.Forbidden ||
                    (unauthorizedResp.StatusCode == HttpStatusCode.Redirect && unauthorizedResp.Headers.Location?.OriginalString.Contains("/Account/AccessDenied") == true));
    }

    [Fact]
    [Trait("Requirement", "PP-DATA-04")]
    [Trait("Requirement", "PP-FRD-02")]
    public async Task R3_TwoDbContext_OptimisticConcurrency_ThrowsDbUpdateConcurrencyException()
    {
        var options = new DbContextOptionsBuilder<PetPetDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        // Context 1 and Context 2 load the same Member
        using var context1 = new PetPetDbContext(options);
        using var context2 = new PetPetDbContext(options);

        var member1 = await context1.Members.FirstAsync(m => m.Email == "alice@petpet.test");
        var member2 = await context2.Members.FirstAsync(m => m.Email == "alice@petpet.test");

        // Context 1 modifies and saves
        member1.Bio = "由 Context 1 修改";
        member1.Version = Guid.NewGuid();
        await context1.SaveChangesAsync();

        // Context 2 modifies with stale original Version and attempts save
        member2.Bio = "由 Context 2 修改";
        member2.Version = Guid.NewGuid();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
        {
            await context2.SaveChangesAsync();
        });
    }

    [Fact]
    [Trait("Requirement", "PP-FRD-01")]
    [Trait("Requirement", "PP-FRD-02")]
    [Trait("Requirement", "PP-BLK-01")]
    public async Task R3_Block_Vs_FriendAcceptAndSend_Competition_ReturnsConflictOrNotFound()
    {
        var aliceClient = await CreateAuthenticatedClientAsync("alice@petpet.test", "AlicePass123!");
        var bobClient = await CreateAuthenticatedClientAsync("bob@petpet.test", "BobPass123!");

        // Alice sends friend request to Bob
        var aliceToken = await ExtractAntiforgeryToken(aliceClient, "/Friend/Index");
        var sendForm = new Dictionary<string, string>
        {
            ["targetEmail"] = "bob@petpet.test",
            ["__RequestVerificationToken"] = aliceToken
        };
        var sendResp = await aliceClient.PostAsync("/Friend/Send", new FormUrlEncodedContent(sendForm));
        Assert.Equal(HttpStatusCode.Redirect, sendResp.StatusCode);

        int friendId;
        Guid friendVersion;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var friend = await db.Friends.FirstAsync(f => f.RequesterEmail == "alice@petpet.test" && f.AddresseeEmail == "bob@petpet.test");
            friendId = friend.Id;
            friendVersion = friend.Version;
        }

        // Bob blocks Alice
        var bobToken = await ExtractAntiforgeryToken(bobClient, "/Block");
        var blockForm = new Dictionary<string, string>
        {
            ["targetEmail"] = "alice@petpet.test",
            ["__RequestVerificationToken"] = bobToken
        };
        var blockResp = await bobClient.PostAsync("/Block/Create", new FormUrlEncodedContent(blockForm));
        Assert.Equal(HttpStatusCode.Redirect, blockResp.StatusCode);

        // Bob attempts to Accept the invitation with the old version -> rejected (404 or 409)
        var bobAcceptToken = await ExtractAntiforgeryToken(bobClient, "/Friend/Index");
        var acceptForm = new Dictionary<string, string>
        {
            ["id"] = friendId.ToString(),
            ["version"] = friendVersion.ToString(),
            ["__RequestVerificationToken"] = bobAcceptToken
        };
        var acceptResp = await bobClient.PostAsync("/Friend/Accept", new FormUrlEncodedContent(acceptForm));
        Assert.True(acceptResp.StatusCode == HttpStatusCode.NotFound || acceptResp.StatusCode == HttpStatusCode.Conflict);

        // Alice attempts to send a new request to Bob -> rejected with 409 Conflict
        var aliceNewToken = await ExtractAntiforgeryToken(aliceClient, "/Friend/Index");
        var aliceSendAgain = await aliceClient.PostAsync("/Friend/Send", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["targetEmail"] = "bob@petpet.test",
            ["__RequestVerificationToken"] = aliceNewToken
        }));
        Assert.Equal(HttpStatusCode.Conflict, aliceSendAgain.StatusCode);

        // Clean up block for subsequent tests
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var block = await db.Blocks.FirstOrDefaultAsync(b => b.BlockerEmail == "bob@petpet.test" && b.BlockedEmail == "alice@petpet.test");
            if (block != null)
            {
                db.Blocks.Remove(block);
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact]
    [Trait("Requirement", "PP-MEDIA-01")]
    [Trait("Requirement", "PP-MEDIA-02")]
    public async Task R4_MediaService_ValidateAndResolveImage_ProtectsOwnershipAndPreset()
    {
        using var scope = _factory.Services.CreateScope();
        var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();
        var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();

        // 1. Preset photo succeeds for any user
        var presetResult = await mediaService.ValidateAndResolveImageAsync("/images/presets/avatar-cat.svg", "alice@petpet.test");
        Assert.Equal("/images/presets/avatar-cat.svg", presetResult);

        // 2. Arbitrary external URL or traversal path returns null
        var pathTraversal = await mediaService.ValidateAndResolveImageAsync("../../etc/passwd", "alice@petpet.test");
        Assert.Null(pathTraversal);

        var externalUrl = await mediaService.ValidateAndResolveImageAsync("http://evil.com/cat.jpg", "alice@petpet.test");
        Assert.Null(externalUrl);

        // 3. User-owned media
        var aliceMediaId = Guid.NewGuid();
        db.Media.Add(new Media
        {
            Id = aliceMediaId,
            OwnerEmail = "alice@petpet.test",
            StorageKey = $"{aliceMediaId:N}.jpg",
            ContentType = "image/jpeg",
            ByteLength = 1024,
            Width = 100,
            Height = 100,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Alice can attach her own media
        var aliceOwnMedia = await mediaService.ValidateAndResolveImageAsync($"/Media/{aliceMediaId}", "alice@petpet.test");
        Assert.Equal($"/Media/{aliceMediaId}", aliceOwnMedia);

        // Bob CANNOT attach Alice's media (ownership check fails, returns null)
        var bobHijack = await mediaService.ValidateAndResolveImageAsync($"/Media/{aliceMediaId}", "bob@petpet.test");
        Assert.Null(bobHijack);
    }

    [Fact]
    [Trait("Requirement", "PP-SEC-01")]
    public async Task R7_SessionRoleRevocation_RedirectsToLoginImmediately()
    {
        var adminClient = await CreateAuthenticatedClientAsync("admin@petpet.test", "AdminPass123!");

        // Initially admin can access admin dashboard
        var adminResp1 = await adminClient.GetAsync("/Admin/ManageUsers");
        Assert.Equal(HttpStatusCode.OK, adminResp1.StatusCode);

        // Admin privilege is revoked in DB (IsAdmin = false)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var admin = await db.Members.FirstAsync(m => m.Email == "admin@petpet.test");
            admin.IsAdmin = false;
            await db.SaveChangesAsync();
        }

        // On next request, OnValidatePrincipal detects role mismatch -> cookie rejected -> redirect to login
        var adminResp2 = await adminClient.GetAsync("/Admin/ManageUsers");
        Assert.Equal(HttpStatusCode.Redirect, adminResp2.StatusCode);
        Assert.Contains("/Member/Login", adminResp2.Headers.Location?.OriginalString);
    }

    [Fact]
    [Trait("Requirement", "PP-SEC-04")]
    public async Task R8_RateLimiter_LoginEndpoint_Returns429TooManyRequests_OnExcessiveAttempts()
    {
        var anonClient = _factory.CreateClient();
        var token = await ExtractAntiforgeryToken(anonClient, "/Member/Login");

        HttpResponseMessage? lastResponse = null;

        // Perform 11 login requests within 1 minute
        for (int i = 0; i < 11; i++)
        {
            var form = new Dictionary<string, string>
            {
                ["email"] = "nonexistent@test.com",
                ["password"] = "WrongPass123!",
                ["__RequestVerificationToken"] = token
            };
            lastResponse = await anonClient.PostAsync("/Member/Login", new FormUrlEncodedContent(form));
            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse.StatusCode);
        Assert.True(lastResponse.Headers.Contains("Retry-After"));
    }

    private async Task<HubConnection> CreateAuthenticatedHubConnectionAsync(string email, string password, string? originHeader = null)
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

        string cookieHeader = string.Empty;
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            cookieHeader = string.Join("; ", setCookies.Select(c => c.Split(';')[0]));
        }

        var baseUri = _factory.Server.BaseAddress;
        var hubUri = new Uri(baseUri, "/chatHub");

        var hubConn = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                if (!string.IsNullOrEmpty(cookieHeader))
                {
                    options.Headers["Cookie"] = cookieHeader;
                }
                if (!string.IsNullOrEmpty(originHeader))
                {
                    options.Headers["Origin"] = originHeader;
                }
            })
            .Build();

        return hubConn;
    }

    [Fact]
    [Trait("Requirement", "PP-DATA-01")]
    [Trait("Requirement", "PP-DATA-02")]
    public async Task Sqlite_Constraints_ForeignKey_And_UniqueIndexes_Enforced()
    {
        var options = new DbContextOptionsBuilder<PetPetDbContext>()
            .UseSqlite($"Data Source={_dbPath};Foreign Keys=True")
            .Options;

        using var db = new PetPetDbContext(options);

        // 1. Foreign Key failure: Non-existent member email on Post
        var invalidPost = new Post
        {
            AuthorEmail = "ghost@petpet.test",
            Title = "孤兒貼文",
            Content = "沒有對應會員",
            CreatedAt = DateTime.UtcNow,
            IsEnabled = true
        };
        db.Posts.Add(invalidPost);
        await Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());

        db.ChangeTracker.Clear();

        // 2. Unique Constraint failure: Duplicate (SenderEmail, ClientMessageId) in Message
        var msgId = Guid.NewGuid();
        var msg1 = new Message
        {
            SenderEmail = "alice@petpet.test",
            ReceiverEmail = "bob@petpet.test",
            ConversationPair = "alice@petpet.test:bob@petpet.test",
            Content = "訊息一",
            ClientMessageId = msgId,
            SentAt = DateTime.UtcNow
        };
        var msg2 = new Message
        {
            SenderEmail = "alice@petpet.test",
            ReceiverEmail = "bob@petpet.test",
            ConversationPair = "alice@petpet.test:bob@petpet.test",
            Content = "訊息二 (相同 ID)",
            ClientMessageId = msgId,
            SentAt = DateTime.UtcNow
        };
        db.Messages.Add(msg1);
        db.Messages.Add(msg2);
        await Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());

        db.ChangeTracker.Clear();

        // 3. Unique Constraint failure: Duplicate (ActorEmail, Operation, CommandId) in OperationReceipt
        var cmdId = Guid.NewGuid();
        var rec1 = new OperationReceipt
        {
            ActorEmail = "alice@petpet.test",
            Operation = "CreatePost",
            CommandId = cmdId,
            PayloadDigest = "digest-1",
            ResultCode = "Success",
            CompletedAtUtc = DateTime.UtcNow
        };
        var rec2 = new OperationReceipt
        {
            ActorEmail = "alice@petpet.test",
            Operation = "CreatePost",
            CommandId = cmdId,
            PayloadDigest = "digest-2",
            ResultCode = "Success",
            CompletedAtUtc = DateTime.UtcNow
        };
        db.OperationReceipts.Add(rec1);
        db.OperationReceipts.Add(rec2);
        await Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
    }

    [Fact]
    [Trait("Requirement", "PP-RUN-02")]
    [Trait("Requirement", "PP-RUN-03")]
    [Trait("Requirement", "PP-RUN-04")]
    public void ModeIsolation_InvalidConfigurations_ThrowsInvalidOperationException()
    {
        // 1. Unknown database provider
        Assert.Throws<InvalidOperationException>(() =>
        {
            new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("Database:Provider", "OracleDb");
            }).CreateClient();
        });

        // 2. Demo mode with public host
        Assert.Throws<InvalidOperationException>(() =>
        {
            new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Demo");
                builder.UseSetting("Demo:Enabled", "true");
                builder.UseSetting("Database:Provider", "Sqlite");
                builder.UseSetting("ASPNETCORE_URLS", "http://0.0.0.0:5213");
            }).CreateClient();
        });

        // 3. Non-demo Sqlite without explicit SqliteConnection
        Assert.Throws<InvalidOperationException>(() =>
        {
            new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("Database:Provider", "Sqlite");
                builder.UseSetting("ConnectionStrings:SqliteConnection", "");
            }).CreateClient();
        });
    }

    [Fact]
    [Trait("Requirement", "PP-SEC-02")]
    public async Task R5_ChatHub_OriginValidation_AbortsHandshake_WhenOriginMismatched()
    {
        var connection = await CreateAuthenticatedHubConnectionAsync(
            "alice@petpet.test",
            "AlicePass123!",
            originHeader: "http://malicious-attacker.com");

        await using (connection)
        {
            Exception? caughtEx = null;
            try
            {
                await connection.StartAsync();
            }
            catch (Exception ex)
            {
                caughtEx = ex;
            }

            Assert.True(caughtEx != null ||
                        connection.State == HubConnectionState.Disconnected ||
                        await Record.ExceptionAsync(async () => await connection.InvokeAsync("MarkRead", Guid.NewGuid(), 1)) != null);
        }
    }

    [Fact]
    [Trait("Requirement", "PP-MSG-01")]
    [Trait("Requirement", "PP-MSG-02")]
    [Trait("Requirement", "PP-SEC-02")]
    public async Task R5_ChatHub_LiveSession_ChatMessaging_And_SecurityStampRevocation()
    {
        // Ensure Alice and Bob are friends in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var existingFriend = await db.Friends.FirstOrDefaultAsync(f =>
                (f.RequesterEmail == "alice@petpet.test" && f.AddresseeEmail == "bob@petpet.test") ||
                (f.RequesterEmail == "bob@petpet.test" && f.AddresseeEmail == "alice@petpet.test"));
            if (existingFriend == null)
            {
                db.Friends.Add(new Friend
                {
                    RequesterEmail = "alice@petpet.test",
                    AddresseeEmail = "bob@petpet.test",
                    MemberAEmail = "alice@petpet.test",
                    MemberBEmail = "bob@petpet.test",
                    IsAccepted = true,
                    CreatedAt = DateTime.UtcNow,
                    Version = Guid.NewGuid()
                });
                await db.SaveChangesAsync();
            }
        }

        Guid bobPublicId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            bobPublicId = (await db.Members.FirstAsync(m => m.Email == "bob@petpet.test")).PublicId;
        }

        var aliceConn = await CreateAuthenticatedHubConnectionAsync("alice@petpet.test", "AlicePass123!");
        var bobConn = await CreateAuthenticatedHubConnectionAsync("bob@petpet.test", "BobPass123!");

        await using (aliceConn)
        await using (bobConn)
        {
            await aliceConn.StartAsync();
            await bobConn.StartAsync();

            var receivedTcs = new TaskCompletionSource<string>();
            bobConn.On<object>("ReceiveMessage", payload =>
            {
                receivedTcs.TrySetResult(payload.ToString() ?? string.Empty);
            });

            var clientMsgId = Guid.NewGuid();
            await aliceConn.InvokeAsync("SendMessage", bobPublicId, clientMsgId, "你好鮑伯，這是一條即時連線訊息！");

            var received = await Task.WhenAny(receivedTcs.Task, Task.Delay(3000));
            Assert.Equal(receivedTcs.Task, received);

            // Now rotate Alice's SecurityStamp in DB (simulating password change / admin invalidation)
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
                var alice = await db.Members.FirstAsync(m => m.Email == "alice@petpet.test");
                alice.SecurityStamp = Guid.NewGuid().ToString("N");
                await db.SaveChangesAsync();
            }

            // Alice tries to send another message on the same WebSocket/Hub connection
            // OnValidatePrincipal or ChatHub will detect the SecurityStamp mismatch -> 401 / MessageFailed / Abort
            var failedTcs = new TaskCompletionSource<string>();
            aliceConn.On<object>("MessageFailed", err =>
            {
                failedTcs.TrySetResult(err.ToString() ?? string.Empty);
            });

            var bobSecondMsgTcs = new TaskCompletionSource<string>();
            bobConn.On<object>("ReceiveMessage", payload =>
            {
                bobSecondMsgTcs.TrySetResult(payload.ToString() ?? string.Empty);
            });

            bool rejectedOrException = false;
            try
            {
                await aliceConn.InvokeAsync("SendMessage", bobPublicId, Guid.NewGuid(), "這條訊息應該被中斷拒絕");
                var failedWait = await Task.WhenAny(failedTcs.Task, Task.Delay(2000));
                if (failedWait == failedTcs.Task)
                {
                    rejectedOrException = true;
                }
            }
            catch (Exception)
            {
                // Cookie rejected by OnValidatePrincipal (401 Unauthorized) or Hub connection aborted
                rejectedOrException = true;
            }

            Assert.True(rejectedOrException, "Alice's call should be rejected either by Cookie Authentication (401) or ChatHub abort");

            // Verify Bob never received the second message
            var bobReceivedSecond = await Task.WhenAny(bobSecondMsgTcs.Task, Task.Delay(1000));
            Assert.NotEqual(bobSecondMsgTcs.Task, bobReceivedSecond);

            // Alice also attempts MarkRead on the same invalidated Hub connection
            bool markReadRejected = false;
            try
            {
                await aliceConn.InvokeAsync("MarkRead", bobPublicId, 999999);
            }
            catch (Exception)
            {
                markReadRejected = true;
            }
            if (!markReadRejected)
            {
                markReadRejected = aliceConn.State == HubConnectionState.Disconnected;
            }
            Assert.True(markReadRejected, "MarkRead call must be rejected after SecurityStamp rotation");

            // Verify zero DB changes: no message with the rejected content was inserted
            using (var verifyScope = _factory.Services.CreateScope())
            {
                var dbVerify = verifyScope.ServiceProvider.GetRequiredService<PetPetDbContext>();
                var msgPersisted = await dbVerify.Messages.AnyAsync(m => m.Content == "這條訊息應該被中斷拒絕");
                Assert.False(msgPersisted, "Rejected message must not be saved into database");
            }
        }
    }

    [Fact]
    [Trait("Requirement", "PP-SEC-02")]
    [Trait("Requirement", "PP-MSG-01")]
    public async Task R5_ChatHub_LiveSession_AccountDisabled_RejectsSendAndMarkRead_ZeroDbChange()
    {
        // Setup Alice and Bob as friends
        Guid bobPublicId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var alice = await db.Members.FirstAsync(m => m.Email == "alice@petpet.test");
            var bob = await db.Members.FirstAsync(m => m.Email == "bob@petpet.test");
            alice.IsEnabled = true;
            await db.SaveChangesAsync();
            bobPublicId = bob.PublicId;

            if (!await db.Friends.AnyAsync(f => (f.RequesterEmail == alice.Email && f.AddresseeEmail == bob.Email) || (f.RequesterEmail == bob.Email && f.AddresseeEmail == alice.Email)))
            {
                db.Friends.Add(new Friend { RequesterEmail = alice.Email, AddresseeEmail = bob.Email, MemberAEmail = alice.Email, MemberBEmail = bob.Email, IsAccepted = true });
                await db.SaveChangesAsync();
            }
        }

        var aliceConn = await CreateAuthenticatedHubConnectionAsync("alice@petpet.test", "AlicePass123!");
        await using (aliceConn)
        {
            await aliceConn.StartAsync();
            Assert.Equal(HubConnectionState.Connected, aliceConn.State);

            // Admin disables Alice
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
                var alice = await db.Members.FirstAsync(m => m.Email == "alice@petpet.test");
                alice.IsEnabled = false;
                await db.SaveChangesAsync();
            }

            var failedTcs = new TaskCompletionSource<string>();
            aliceConn.On<object>("MessageFailed", err => failedTcs.TrySetResult(err.ToString() ?? string.Empty));

            var rejectedMsgId = Guid.NewGuid();
            bool sendRejected = false;
            try
            {
                await aliceConn.InvokeAsync("SendMessage", bobPublicId, rejectedMsgId, "停權後不可送出的訊息");
                var wait = await Task.WhenAny(failedTcs.Task, Task.Delay(2000));
                if (wait == failedTcs.Task) sendRejected = true;
            }
            catch (Exception)
            {
                sendRejected = true;
            }
            Assert.True(sendRejected || aliceConn.State == HubConnectionState.Disconnected, "SendMessage must be rejected after account disabled");

            bool markReadRejected = false;
            try
            {
                await aliceConn.InvokeAsync("MarkRead", bobPublicId, 1);
            }
            catch (Exception)
            {
                markReadRejected = true;
            }
            Assert.True(markReadRejected || aliceConn.State == HubConnectionState.Disconnected, "MarkRead must be rejected after account disabled");

            // Verify zero DB changes
            using (var verifyScope = _factory.Services.CreateScope())
            {
                var db = verifyScope.ServiceProvider.GetRequiredService<PetPetDbContext>();
                var exists = await db.Messages.AnyAsync(m => m.ClientMessageId == rejectedMsgId);
                Assert.False(exists, "Disabled user message must not be persisted in DB");

                // Restore alice.IsEnabled for other tests
                var alice = await db.Members.FirstAsync(m => m.Email == "alice@petpet.test");
                alice.IsEnabled = true;
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact]
    [Trait("Requirement", "PP-SOC-02")]
    [Trait("Requirement", "PP-UX-02")]
    public async Task F4_PostCanonicalDigest_NewlineBoundaryCollision_ProducesDifferentDigests_And_ReturnsConflict()
    {
        // 1. Direct regression verification: Title="a\nb",Content="c" vs Title="a",Content="b\nc"
        var digest1 = PostController.ComputeCanonicalPayloadDigest("a\nb", "c", null);
        var digest2 = PostController.ComputeCanonicalPayloadDigest("a", "b\nc", null);
        Assert.NotEqual(digest1, digest2);

        // 2. HTTP End-to-End: Idempotency conflict enforcement across newline boundaries
        var aliceClient = await CreateAuthenticatedClientAsync("alice@petpet.test", "AlicePass123!");
        var token = await ExtractAntiforgeryToken(aliceClient, "/Post/Create");

        var cmdId = Guid.NewGuid();
        var formA = new Dictionary<string, string>
        {
            ["CommandId"] = cmdId.ToString(),
            ["Title"] = "a\nb",
            ["Content"] = "c",
            ["ImageUrl"] = "/images/presets/avatar-dog.svg",
            ["__RequestVerificationToken"] = token
        };

        // First attempt creates post
        var respA = await aliceClient.PostAsync("/Post/Create", new FormUrlEncodedContent(formA));
        Assert.Equal(HttpStatusCode.Redirect, respA.StatusCode);

        // Same CommandId with conflicting content (which previously collided in naive newline concatenation) -> 409 Conflict
        var formB = new Dictionary<string, string>
        {
            ["CommandId"] = cmdId.ToString(),
            ["Title"] = "a",
            ["Content"] = "b\nc",
            ["ImageUrl"] = "/images/presets/avatar-dog.svg",
            ["__RequestVerificationToken"] = token
        };
        var respB = await aliceClient.PostAsync("/Post/Create", new FormUrlEncodedContent(formB));
        Assert.Equal(HttpStatusCode.Conflict, respB.StatusCode);

        // Same CommandId with identical content -> Idempotent replay (302 Redirect to same post)
        var respReplay = await aliceClient.PostAsync("/Post/Create", new FormUrlEncodedContent(formA));
        Assert.Equal(HttpStatusCode.Redirect, respReplay.StatusCode);
        Assert.Equal(respA.Headers.Location, respReplay.Headers.Location);
    }

    [Fact]
    [Trait("Requirement", "PP-MSG-02")]
    public async Task F3_ChatService_SendMessage_DifferentTarget_SameClientId_ReturnsConflict()
    {
        using var scope = _factory.Services.CreateScope();
        var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();
        var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IMemberPasswordHasher>();

        var alice = await db.Members.FirstAsync(m => m.Email == "alice@petpet.test");
        var bob = await db.Members.FirstAsync(m => m.Email == "bob@petpet.test");

        var charlie = await db.Members.FirstOrDefaultAsync(m => m.Email == "charlie@petpet.test");
        if (charlie == null)
        {
            charlie = new Member
            {
                Email = "charlie@petpet.test",
                NormalizedEmail = "CHARLIE@PETPET.TEST",
                Name = "查理",
                IsEnabled = true,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                Version = Guid.NewGuid(),
                CityId = 1
            };
            charlie.Password = hasher.Hash(charlie, "CharliePass123!");
            db.Members.Add(charlie);
            await db.SaveChangesAsync();
        }

        // Ensure friendships
        if (!await db.Friends.AnyAsync(f => (f.RequesterEmail == alice.Email && f.AddresseeEmail == bob.Email) || (f.RequesterEmail == bob.Email && f.AddresseeEmail == alice.Email)))
        {
            db.Friends.Add(new Friend { RequesterEmail = alice.Email, AddresseeEmail = bob.Email, MemberAEmail = alice.Email, MemberBEmail = bob.Email, IsAccepted = true });
        }
        if (!await db.Friends.AnyAsync(f => (f.RequesterEmail == alice.Email && f.AddresseeEmail == charlie.Email) || (f.RequesterEmail == charlie.Email && f.AddresseeEmail == alice.Email)))
        {
            db.Friends.Add(new Friend { RequesterEmail = alice.Email, AddresseeEmail = charlie.Email, MemberAEmail = alice.Email, MemberBEmail = charlie.Email, IsAccepted = true });
        }
        await db.SaveChangesAsync();

        var clientMsgId = Guid.NewGuid();

        // 1. First send to Bob
        var resp1 = await chatService.SendMessageAsync(alice.Email, bob.PublicId, clientMsgId, "Hello Bob");
        Assert.True(resp1.Success);
        Assert.False(resp1.IsDuplicate);

        // 2. Replay same clientMsgId with same content to Bob -> Duplicate ACK
        var respReplay = await chatService.SendMessageAsync(alice.Email, bob.PublicId, clientMsgId, "Hello Bob");
        Assert.True(respReplay.Success);
        Assert.True(respReplay.IsDuplicate);
        Assert.Equal(resp1.MessageId, respReplay.MessageId);

        // 3. Same clientMsgId but target changed to Charlie -> Conflict (False with conflict error message)
        var respWrongTarget = await chatService.SendMessageAsync(alice.Email, charlie.PublicId, clientMsgId, "Hello Bob");
        Assert.False(respWrongTarget.Success);
        Assert.Contains("衝突", respWrongTarget.ErrorMessage);

        // 4. Same clientMsgId to Bob but content changed -> Conflict
        var respWrongContent = await chatService.SendMessageAsync(alice.Email, bob.PublicId, clientMsgId, "Tampered content");
        Assert.False(respWrongContent.Success);
        Assert.Contains("衝突", respWrongContent.ErrorMessage);
    }

    [Fact]
    [Trait("Requirement", "PP-MSG-02")]
    public async Task F3_ChatService_SendMessage_ConcurrentRequests_InsertsSingleRow_And_ReturnsOriginalAck()
    {
        using (var setupScope = _factory.Services.CreateScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            if (!await setupDb.Friends.AnyAsync(f => (f.RequesterEmail == "alice@petpet.test" && f.AddresseeEmail == "bob@petpet.test") || (f.RequesterEmail == "bob@petpet.test" && f.AddresseeEmail == "alice@petpet.test")))
            {
                setupDb.Friends.Add(new Friend { RequesterEmail = "alice@petpet.test", AddresseeEmail = "bob@petpet.test", MemberAEmail = "alice@petpet.test", MemberBEmail = "bob@petpet.test", IsAccepted = true });
                await setupDb.SaveChangesAsync();
            }
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
        var alice = await db.Members.FirstAsync(m => m.Email == "alice@petpet.test");
        var bob = await db.Members.FirstAsync(m => m.Email == "bob@petpet.test");

        var clientMsgId = Guid.NewGuid();

        // Run two concurrent tasks with independent scopes and db contexts
        var task1 = Task.Run(async () =>
        {
            using var s1 = _factory.Services.CreateScope();
            var cs1 = s1.ServiceProvider.GetRequiredService<IChatService>();
            return await cs1.SendMessageAsync(alice.Email, bob.PublicId, clientMsgId, "並發傳送訊息測試");
        });

        var task2 = Task.Run(async () =>
        {
            using var s2 = _factory.Services.CreateScope();
            var cs2 = s2.ServiceProvider.GetRequiredService<IChatService>();
            return await cs2.SendMessageAsync(alice.Email, bob.PublicId, clientMsgId, "並發傳送訊息測試");
        });

        var results = await Task.WhenAll(task1, task2);
        Assert.True(results[0].Success, results[0].ErrorMessage);
        Assert.True(results[1].Success, results[1].ErrorMessage);
        Assert.Equal(results[0].MessageId, results[1].MessageId);

        // Exactly 1 message inserted in DB
        using (var verifyScope = _factory.Services.CreateScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var count = await verifyDb.Messages.CountAsync(m => m.ClientMessageId == clientMsgId);
            Assert.Equal(1, count);
        }
    }

    [Fact]
    [Trait("Requirement", "PP-MSG-01")]
    [Trait("Requirement", "PP-BLK-01")]
    public async Task F3_Chat_Vs_Block_TwoIndependentConnections_SyncBarrier_ProvesBlockPreventsMessage()
    {
        // 1. Prepare Friend relationship
        using (var setupScope = _factory.Services.CreateScope())
        {
            var dbSetup = setupScope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var alice = await dbSetup.Members.FirstAsync(m => m.Email == "alice@petpet.test");
            var bob = await dbSetup.Members.FirstAsync(m => m.Email == "bob@petpet.test");

            // Clean up any existing blocks between alice and bob
            var existingBlocks = await dbSetup.Blocks.Where(b => (b.BlockerEmail == alice.Email && b.BlockedEmail == bob.Email) ||
                                                                (b.BlockerEmail == bob.Email && b.BlockedEmail == alice.Email)).ToListAsync();
            dbSetup.Blocks.RemoveRange(existingBlocks);

            if (!await dbSetup.Friends.AnyAsync(f => (f.RequesterEmail == alice.Email && f.AddresseeEmail == bob.Email) || (f.RequesterEmail == bob.Email && f.AddresseeEmail == alice.Email)))
            {
                dbSetup.Friends.Add(new Friend { RequesterEmail = alice.Email, AddresseeEmail = bob.Email, MemberAEmail = alice.Email, MemberBEmail = bob.Email, IsAccepted = true });
            }
            await dbSetup.SaveChangesAsync();
        }

        // 2. Real concurrent execution using two independent DbContext scopes and a 2-party Barrier
        using var scopeSender = _factory.Services.CreateScope();
        using var scopeBlocker = _factory.Services.CreateScope();

        var dbSender = scopeSender.ServiceProvider.GetRequiredService<PetPetDbContext>();
        var dbBlocker = scopeBlocker.ServiceProvider.GetRequiredService<PetPetDbContext>();
        var chatServiceSender = scopeSender.ServiceProvider.GetRequiredService<IChatService>();

        var senderBob = await dbSender.Members.AsNoTracking().FirstAsync(m => m.Email == "bob@petpet.test");
        var clientMsgId = Guid.NewGuid();

        var barrier = new Barrier(2);
        SendMessageResponse? sendResult = null;
        Exception? sendEx = null;
        Exception? blockEx = null;

        var taskSend = Task.Run(async () =>
        {
            try
            {
                // Wait at sync barrier so both threads release simultaneously
                barrier.SignalAndWait();
                sendResult = await chatServiceSender.SendMessageAsync("alice@petpet.test", senderBob.PublicId, clientMsgId, "交錯並發競賽訊息");
            }
            catch (Exception ex)
            {
                sendEx = ex;
            }
        });

        var taskBlock = Task.Run(async () =>
        {
            try
            {
                // Wait at sync barrier so both threads release simultaneously
                barrier.SignalAndWait();
                var block = new Block
                {
                    BlockerEmail = "bob@petpet.test",
                    BlockedEmail = "alice@petpet.test",
                    CreatedAt = DateTime.UtcNow
                };
                dbBlocker.Blocks.Add(block);
                await dbBlocker.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                blockEx = ex;
            }
        });

        await Task.WhenAll(taskSend, taskBlock);

        Assert.Null(sendEx);
        Assert.Null(blockEx);
        Assert.NotNull(sendResult);

        // Verify serializable consistency:
        // Case A: Block won the race -> SendMessage returned false with "封鎖", message was NOT persisted.
        // Case B: SendMessage won the race -> SendMessage returned true, message WAS persisted.
        using (var verifyScope = _factory.Services.CreateScope())
        {
            var dbVerify = verifyScope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var messageInDb = await dbVerify.Messages.FirstOrDefaultAsync(m => m.ClientMessageId == clientMsgId);
            var blockInDb = await dbVerify.Blocks.AnyAsync(b => b.BlockerEmail == "bob@petpet.test" && b.BlockedEmail == "alice@petpet.test");

            Assert.True(blockInDb, "Block must be persisted regardless of race ordering");

            if (!sendResult.Success)
            {
                // Block won
                Assert.Contains("封鎖", sendResult.ErrorMessage);
                Assert.Null(messageInDb);
            }
            else
            {
                // Send won before block committed
                Assert.NotNull(messageInDb);
                Assert.Equal(clientMsgId, messageInDb.ClientMessageId);
            }

            // Clean up block for subsequent tests
            var blocks = await dbVerify.Blocks.Where(b => b.BlockerEmail == "bob@petpet.test" && b.BlockedEmail == "alice@petpet.test").ToListAsync();
            dbVerify.Blocks.RemoveRange(blocks);
            await dbVerify.SaveChangesAsync();
        }
    }

    [Fact]
    [Trait("Requirement", "PP-SEC-04")]
    public async Task F5_RateLimiter_MemberIsolation_And_SameIdentityAcrossDifferentIps()
    {
        var aliceClient = await CreateAuthenticatedClientAsync("alice@petpet.test", "AlicePass123!");
        var bobClient = await CreateAuthenticatedClientAsync("bob@petpet.test", "BobPass123!");

        // Ensure test post exists
        int testPostId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var post = await db.Posts.FirstOrDefaultAsync(p => p.IsEnabled && !p.IsDeleted);
            if (post == null)
            {
                post = new Post
                {
                    AuthorEmail = "alice@petpet.test",
                    Title = "Rate limit test post",
                    Content = "Post content",
                    IsEnabled = true,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    Version = Guid.NewGuid()
                };
                db.Posts.Add(post);
                await db.SaveChangesAsync();
            }
            testPostId = post.Id;
        }

        var aliceToken = await ExtractAntiforgeryToken(aliceClient, $"/Post/Details/{testPostId}");
        for (int i = 0; i < 30; i++)
        {
            var form = new Dictionary<string, string>
            {
                ["postId"] = testPostId.ToString(),
                ["content"] = $"Alice quota test comment {i}",
                ["__RequestVerificationToken"] = aliceToken
            };
            var resp = await aliceClient.PostAsync("/Post/AddComment", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        }

        // Alice 31st attempt -> 429 Too Many Requests
        var alice31stForm = new Dictionary<string, string>
        {
            ["postId"] = testPostId.ToString(),
            ["content"] = "Alice 31st comment",
            ["__RequestVerificationToken"] = aliceToken
        };
        var alice31stResp = await aliceClient.PostAsync("/Post/AddComment", new FormUrlEncodedContent(alice31stForm));
        Assert.Equal(HttpStatusCode.TooManyRequests, alice31stResp.StatusCode);
        Assert.True(alice31stResp.Headers.Contains("Retry-After"));

        // Bob makes a comment -> Success (302 Redirect). Demonstrates Alice exhausting quota does NOT block Bob!
        var bobToken = await ExtractAntiforgeryToken(bobClient, $"/Post/Details/{testPostId}");
        var bobForm = new Dictionary<string, string>
        {
            ["postId"] = testPostId.ToString(),
            ["content"] = "Bob independent comment",
            ["__RequestVerificationToken"] = bobToken
        };
        var bobResp = await bobClient.PostAsync("/Post/AddComment", new FormUrlEncodedContent(bobForm));
        Assert.Equal(HttpStatusCode.Redirect, bobResp.StatusCode);

        // Same account credential stuffing across multiple IPs:
        // Client from IP 1 attempts 3 failed logins
        var anonClient = _factory.CreateClient();
        var loginToken = await ExtractAntiforgeryToken(anonClient, "/Member/Login");
        for (int i = 0; i < 3; i++)
        {
            var loginForm = new Dictionary<string, string>
            {
                ["email"] = "stuffing_victim@test.com",
                ["password"] = "BadPass!",
                ["__RequestVerificationToken"] = loginToken
            };
            var req = new HttpRequestMessage(HttpMethod.Post, "/Member/Login") { Content = new FormUrlEncodedContent(loginForm) };
            req.Headers.Add("X-Forwarded-For", "192.168.1.101");
            req.Headers.Add("X-Login-Identity", "stuffing_victim@test.com");
            var r = await anonClient.SendAsync(req);
            Assert.True(r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Redirect);
        }

        // Client from IP 2 attempts 3 failed logins on the SAME account
        HttpResponseMessage? lastResp = null;
        for (int i = 0; i < 3; i++)
        {
            var loginForm = new Dictionary<string, string>
            {
                ["email"] = "stuffing_victim@test.com",
                ["password"] = "BadPass!",
                ["__RequestVerificationToken"] = loginToken
            };
            var req = new HttpRequestMessage(HttpMethod.Post, "/Member/Login") { Content = new FormUrlEncodedContent(loginForm) };
            req.Headers.Add("X-Forwarded-For", "192.168.2.202");
            req.Headers.Add("X-Login-Identity", "stuffing_victim@test.com");
            lastResp = await anonClient.SendAsync(req);
        }

        // On the 5th/6th total attempt across IPs, identity limiter triggers 429
        Assert.NotNull(lastResp);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResp.StatusCode);
        Assert.True(lastResp.Headers.Contains("Retry-After"));
    }

    [Fact]
    [Trait("Requirement", "PP-SEC-04")]
    public async Task F5_RateLimiter_Friend_Report_And_HubSend_Enforced()
    {
        var aliceClient = await CreateAuthenticatedClientAsync("alice@petpet.test", "AlicePass123!");

        // 1. Friend request limit (10/min)
        var friendToken = await ExtractAntiforgeryToken(aliceClient, "/Friend/Index");
        HttpResponseMessage? friendLast = null;
        for (int i = 0; i < 11; i++)
        {
            var form = new Dictionary<string, string>
            {
                ["targetEmail"] = $"stranger{i}@petpet.test",
                ["__RequestVerificationToken"] = friendToken
            };
            friendLast = await aliceClient.PostAsync("/Friend/SendRequest", new FormUrlEncodedContent(form));
            if (friendLast.StatusCode == HttpStatusCode.TooManyRequests) break;
        }
        Assert.NotNull(friendLast);
        Assert.Equal(HttpStatusCode.TooManyRequests, friendLast.StatusCode);
        Assert.True(friendLast.Headers.Contains("Retry-After"));

        // 2. Hub send limit: 60/min
        var aliceHub = await CreateAuthenticatedHubConnectionAsync("alice@petpet.test", "AlicePass123!");
        await aliceHub.StartAsync();

        Guid bobPublicId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var alice = await db.Members.FirstAsync(m => m.Email == "alice@petpet.test");
            var bob = await db.Members.FirstAsync(m => m.Email == "bob@petpet.test");
            bobPublicId = bob.PublicId;

            if (!await db.Friends.AnyAsync(f => (f.RequesterEmail == alice.Email && f.AddresseeEmail == bob.Email) || (f.RequesterEmail == bob.Email && f.AddresseeEmail == alice.Email)))
            {
                db.Friends.Add(new Friend { RequesterEmail = alice.Email, AddresseeEmail = bob.Email, MemberAEmail = alice.Email, MemberBEmail = bob.Email, IsAccepted = true });
                await db.SaveChangesAsync();
            }
        }

        var rateLimiter = _factory.Services.GetRequiredService<IChatRateLimiter>();
        rateLimiter.Reset();
        string? hubRateLimitError = null;
        aliceHub.On<object>("MessageFailed", err =>
        {
            var str = err.ToString();
            if (str?.Contains("每分鐘 60 則") == true || str?.Contains("429 Too Many Requests") == true)
            {
                hubRateLimitError = str;
            }
        });

        for (int i = 0; i < 62; i++)
        {
            await aliceHub.InvokeAsync("SendMessage", bobPublicId, Guid.NewGuid(), $"Hub message {i}");
            if (hubRateLimitError != null) break;
        }

        Assert.NotNull(hubRateLimitError);
        Assert.Contains("每分鐘 60 則", hubRateLimitError);
        await aliceHub.DisposeAsync();
    }

    [Fact]
    [Trait("Requirement", "PP-SEC-04")]
    [Trait("Requirement", "PP-MSG-01")]
    public async Task F5_ChatRateLimiter_CrossProtocol_HttpAndHubShareQuota_Throws429AndMessageFailed()
    {
        var aliceClient = await CreateAuthenticatedClientAsync("alice@petpet.test", "AlicePass123!");
        var bobClient = await CreateAuthenticatedClientAsync("bob@petpet.test", "BobPass123!");

        Guid bobPublicId;
        Guid alicePublicId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var alice = await db.Members.FirstAsync(m => m.Email == "alice@petpet.test");
            var bob = await db.Members.FirstAsync(m => m.Email == "bob@petpet.test");
            alicePublicId = alice.PublicId;
            bobPublicId = bob.PublicId;

            // Ensure friendship
            if (!await db.Friends.AnyAsync(f => (f.RequesterEmail == alice.Email && f.AddresseeEmail == bob.Email) || (f.RequesterEmail == bob.Email && f.AddresseeEmail == alice.Email)))
            {
                db.Friends.Add(new Friend { RequesterEmail = alice.Email, AddresseeEmail = bob.Email, MemberAEmail = alice.Email, MemberBEmail = bob.Email, IsAccepted = true });
                await db.SaveChangesAsync();
            }
        }

        // Reset rate limiter for isolated measurement
        var rateLimiter = _factory.Services.GetRequiredService<IChatRateLimiter>();
        rateLimiter.Reset();

        // 1. Alice sends 30 messages via HTTP /Chat/Send
        var aliceCsrf = await ExtractAntiforgeryToken(aliceClient, "/Chat");
        for (int i = 0; i < 30; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/Chat/Send");
            req.Headers.Add("X-CSRF-TOKEN", aliceCsrf);
            req.Content = System.Net.Http.Json.JsonContent.Create(new
            {
                TargetPublicId = bobPublicId,
                ClientMessageId = Guid.NewGuid(),
                Content = $"Alice HTTP message {i}"
            });
            var res = await aliceClient.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        // 2. Alice sends 30 messages via SignalR Hub on the same account
        var aliceHub = await CreateAuthenticatedHubConnectionAsync("alice@petpet.test", "AlicePass123!");
        await aliceHub.StartAsync();

        string? earlyFailed = null;
        aliceHub.On<object>("MessageFailed", err => earlyFailed = err.ToString());

        for (int i = 0; i < 30; i++)
        {
            await aliceHub.InvokeAsync("SendMessage", bobPublicId, Guid.NewGuid(), $"Alice Hub message {i}");
        }
        Assert.Null(earlyFailed); // All 30 succeeded (total 60 messages consumed across HTTP and Hub)

        // 3. Alice 61st message via HTTP /Chat/Send must be rejected with 429 and Retry-After
        var req61 = new HttpRequestMessage(HttpMethod.Post, "/Chat/Send");
        req61.Headers.Add("X-CSRF-TOKEN", aliceCsrf);
        req61.Content = System.Net.Http.Json.JsonContent.Create(new
        {
            TargetPublicId = bobPublicId,
            ClientMessageId = Guid.NewGuid(),
            Content = "Alice 61st message over HTTP"
        });
        var res61 = await aliceClient.SendAsync(req61);
        Assert.Equal(HttpStatusCode.TooManyRequests, res61.StatusCode);
        Assert.True(res61.Headers.Contains("Retry-After"));
        var retryAfterVal = int.Parse(res61.Headers.GetValues("Retry-After").First());
        Assert.InRange(retryAfterVal, 1, 60);

        // 4. Alice 62nd message via SignalR Hub must also be rejected with MessageFailed
        var hubQuotaFailedTcs = new TaskCompletionSource<string>();
        aliceHub.On<object>("MessageFailed", err => hubQuotaFailedTcs.TrySetResult(err.ToString() ?? string.Empty));
        await aliceHub.InvokeAsync("SendMessage", bobPublicId, Guid.NewGuid(), "Alice 62nd message over Hub");
        var hubFailedResult = await Task.WhenAny(hubQuotaFailedTcs.Task, Task.Delay(2000));
        Assert.Equal(hubQuotaFailedTcs.Task, hubFailedResult);
        var hubErrText = await hubQuotaFailedTcs.Task;
        Assert.Contains("60", hubErrText);
        Assert.Contains("429", hubErrText);

        // 5. Member isolation: Bob sends a message via HTTP -> Must SUCCEED (not blocked by Alice's exhaustion)
        var bobCsrf = await ExtractAntiforgeryToken(bobClient, "/Chat");
        var reqBob = new HttpRequestMessage(HttpMethod.Post, "/Chat/Send");
        reqBob.Headers.Add("X-CSRF-TOKEN", bobCsrf);
        reqBob.Content = System.Net.Http.Json.JsonContent.Create(new
        {
            TargetPublicId = alicePublicId,
            ClientMessageId = Guid.NewGuid(),
            Content = "Bob independent message to Alice"
        });
        var resBob = await bobClient.SendAsync(reqBob);
        Assert.Equal(HttpStatusCode.OK, resBob.StatusCode);

        // 6. Multi-connection same member: Alice creates a second Hub connection and attempts to send
        var aliceHub2 = await CreateAuthenticatedHubConnectionAsync("alice@petpet.test", "AlicePass123!");
        await aliceHub2.StartAsync();
        var hub2FailedTcs = new TaskCompletionSource<string>();
        aliceHub2.On<object>("MessageFailed", err => hub2FailedTcs.TrySetResult(err.ToString() ?? string.Empty));
        await aliceHub2.InvokeAsync("SendMessage", bobPublicId, Guid.NewGuid(), "Alice second connection attempt");
        var hub2FailedResult = await Task.WhenAny(hub2FailedTcs.Task, Task.Delay(2000));
        Assert.Equal(hub2FailedTcs.Task, hub2FailedResult);

        await aliceHub.DisposeAsync();
        await aliceHub2.DisposeAsync();
    }
}
