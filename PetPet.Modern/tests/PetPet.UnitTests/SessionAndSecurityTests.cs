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

public class SessionAndSecurityTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _sqliteConnection;
    private readonly WebApplicationFactory<Program> _factory;

    public SessionAndSecurityTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"petpet_sec_{Guid.NewGuid():N}.db");
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

        // Initialize schema
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
        db.Database.EnsureCreated();
        SeedInitialUsers(db, scope.ServiceProvider.GetRequiredService<IMemberPasswordHasher>());
    }

    private static void SeedInitialUsers(PetPetDbContext db, IMemberPasswordHasher hasher)
    {
        var admin = new Member
        {
            Email = "admin@petpet.test",
            NormalizedEmail = "ADMIN@PETPET.TEST",
            Name = "系統管理員",
            Password = string.Empty,
            IsAdmin = true,
            IsEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Version = Guid.NewGuid()
        };
        admin.Password = hasher.Hash(admin, "AdminPass123!");

        var user = new Member
        {
            Email = "user@petpet.test",
            NormalizedEmail = "USER@PETPET.TEST",
            Name = "一般會員",
            Password = string.Empty,
            IsAdmin = false,
            IsEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Version = Guid.NewGuid()
        };
        user.Password = hasher.Hash(user, "UserPass123!");

        db.Members.AddRange(admin, user);
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
    [Trait("Requirement", "PP-SEC-01")]
    public async Task DisabledUser_SessionRevocation_RedirectsToLoginImmediately()
    {
        var userClient = await CreateAuthenticatedClientAsync("user@petpet.test", "UserPass123!");

        // Initial check: User can access protected page
        var petResp1 = await userClient.GetAsync("/Pet/Index");
        Assert.Equal(HttpStatusCode.OK, petResp1.StatusCode);

        // Admin disables this user in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var user = await db.Members.FirstAsync(m => m.Email == "user@petpet.test");
            user.IsEnabled = false;
            await db.SaveChangesAsync();
        }

        // User next request must be rejected and redirected to login
        var petResp2 = await userClient.GetAsync("/Pet/Index");
        Assert.Equal(HttpStatusCode.Redirect, petResp2.StatusCode);
        Assert.Contains("/Member/Login", petResp2.Headers.Location?.OriginalString);
    }

    [Fact]
    [Trait("Requirement", "PP-SEC-02")]
    public async Task RotatedSecurityStamp_SessionRevocation_RedirectsToLoginImmediately()
    {
        var userClient = await CreateAuthenticatedClientAsync("user@petpet.test", "UserPass123!");

        var petResp1 = await userClient.GetAsync("/Pet/Index");
        Assert.Equal(HttpStatusCode.OK, petResp1.StatusCode);

        // Security stamp is rotated (e.g. password changed or token invalidated)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var user = await db.Members.FirstAsync(m => m.Email == "user@petpet.test");
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();
        }

        var petResp2 = await userClient.GetAsync("/Pet/Index");
        Assert.Equal(HttpStatusCode.Redirect, petResp2.StatusCode);
        Assert.Contains("/Member/Login", petResp2.Headers.Location?.OriginalString);
    }

    [Fact]
    [Trait("Requirement", "PP-SEC-03")]
    public async Task PostRequest_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        var userClient = await CreateAuthenticatedClientAsync("user@petpet.test", "UserPass123!");

        // POST without Antiforgery token
        var postResp = await userClient.PostAsync("/Pet/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "柴柴",
            ["KindCode"] = "Dog"
        }));

        Assert.Equal(HttpStatusCode.BadRequest, postResp.StatusCode);
    }

    [Fact]
    [Trait("Requirement", "PP-SEC-04")]
    public async Task Admin_CannotDisableSelf_AndCannotDisableLastActiveAdmin()
    {
        var adminClient = await CreateAuthenticatedClientAsync("admin@petpet.test", "AdminPass123!");
        var token = await ExtractAntiforgeryToken(adminClient, "/Admin/ManageUsers");

        Guid adminVersion;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var admin = await db.Members.FirstAsync(m => m.Email == "admin@petpet.test");
            adminVersion = admin.Version;
        }

        // Admin attempts to disable self
        var selfDisableResp = await adminClient.PostAsync("/Admin/SetUserEnabled", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "admin@petpet.test",
            ["enabled"] = "false",
            ["version"] = adminVersion.ToString(),
            ["__RequestVerificationToken"] = token
        }));

        // Expect 409 Conflict
        Assert.Equal(HttpStatusCode.Conflict, selfDisableResp.StatusCode);
    }
}
