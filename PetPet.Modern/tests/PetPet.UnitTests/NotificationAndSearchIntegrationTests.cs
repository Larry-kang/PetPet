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

public class NotificationAndSearchIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _sqliteConnection;
    private readonly WebApplicationFactory<Program> _factory;

    public NotificationAndSearchIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"petpet_notif_{Guid.NewGuid():N}.db");
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
        var city1 = new City { Id = 1, Name = "臺北市", Code = "TPE" };
        var city2 = new City { Id = 2, Name = "高雄市", Code = "KHH" };
        db.Cities.AddRange(city1, city2);

        var alice = new Member
        {
            Email = "alice@notif.test",
            NormalizedEmail = "ALICE@NOTIF.TEST",
            PublicId = Guid.NewGuid(),
            Name = "Alice",
            CityId = 1,
            Password = string.Empty,
            IsEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Version = Guid.NewGuid()
        };
        alice.Password = hasher.Hash(alice, "AlicePass123!");

        var bob = new Member
        {
            Email = "bob@notif.test",
            NormalizedEmail = "BOB@NOTIF.TEST",
            PublicId = Guid.NewGuid(),
            Name = "Bob",
            CityId = 2,
            Password = string.Empty,
            IsEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Version = Guid.NewGuid()
        };
        bob.Password = hasher.Hash(bob, "BobPass123!");

        db.Members.AddRange(alice, bob);

        // Posts
        var post1 = new Post
        {
            AuthorEmail = "alice@notif.test",
            Title = "超級可愛柴犬日常分享",
            Content = "今天帶柴犬去公園散步！",
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            IsEnabled = true,
            IsDeleted = false,
            Version = Guid.NewGuid()
        };

        var post2 = new Post
        {
            AuthorEmail = "bob@notif.test",
            Title = "高雄散步景點推薦",
            Content = "中央公園很適合溜狗！",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            IsEnabled = true,
            IsDeleted = false,
            Version = Guid.NewGuid()
        };

        db.Posts.AddRange(post1, post2);
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
    [Trait("Requirement", "PP-NOT-01")]
    public async Task Notifications_TriggerOnFriendRequest_AndMarkRead_Idempotent()
    {
        var aliceClient = await CreateAuthenticatedClientAsync("alice@notif.test", "AlicePass123!");
        var bobClient = await CreateAuthenticatedClientAsync("bob@notif.test", "BobPass123!");

        // Alice sends friend request to Bob
        var aliceToken = await ExtractAntiforgeryToken(aliceClient, "/Friend/Index");
        var sendResp = await aliceClient.PostAsync("/Friend/SendRequest", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["targetEmail"] = "bob@notif.test",
            ["__RequestVerificationToken"] = aliceToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, sendResp.StatusCode);

        // Bob should have an unread notification
        int notifId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var notif = await db.Notifications.FirstOrDefaultAsync(n => n.RecipientEmail == "bob@notif.test" && n.Type == "friend_requested");
            Assert.NotNull(notif);
            Assert.Null(notif.ReadAt);
            notifId = notif.Id;
        }

        // Bob marks notification as read
        var bobToken = await ExtractAntiforgeryToken(bobClient, "/Notification/Index");
        var markResp = await bobClient.PostAsync("/Notification/MarkRead", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = notifId.ToString(),
            ["__RequestVerificationToken"] = bobToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, markResp.StatusCode);

        DateTime firstReadTime;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var notif = await db.Notifications.FindAsync(notifId);
            Assert.NotNull(notif);
            Assert.NotNull(notif.ReadAt);
            firstReadTime = notif.ReadAt.Value;
        }

        // Bob marks read again (idempotent, does not error)
        var markResp2 = await bobClient.PostAsync("/Notification/MarkRead", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = notifId.ToString(),
            ["__RequestVerificationToken"] = bobToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, markResp2.StatusCode);
    }

    [Fact]
    [Trait("Requirement", "PP-SOC-05")]
    public async Task Search_LiteralContains_FiltersCity_Sorts()
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        // 1. Search keyword "柴犬"
        var qResp = await client.GetAsync("/Search?q=柴犬");
        Assert.Equal(HttpStatusCode.OK, qResp.StatusCode);
        var qHtml = await qResp.Content.ReadAsStringAsync();
        Assert.Contains("超級可愛柴犬日常分享", qHtml);
        Assert.DoesNotContain("高雄散步景點推薦", qHtml);

        // 2. Filter cityId = 2 (高雄市)
        var cityResp = await client.GetAsync("/Search?cityId=2");
        Assert.Equal(HttpStatusCode.OK, cityResp.StatusCode);
        var cityHtml = await cityResp.Content.ReadAsStringAsync();
        Assert.Contains("高雄散步景點推薦", cityHtml);
        Assert.DoesNotContain("超級可愛柴犬日常分享", cityHtml);

        // 3. Search non-existent keyword
        var noneResp = await client.GetAsync("/Search?q=不存在的關鍵字xyz");
        Assert.Equal(HttpStatusCode.OK, noneResp.StatusCode);
        var noneHtml = await noneResp.Content.ReadAsStringAsync();
        Assert.Contains("找不到符合條件的貼文", noneHtml);
    }
}
