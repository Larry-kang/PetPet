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

public class ProfileAndPetIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _sqliteConnection;
    private readonly WebApplicationFactory<Program> _factory;

    public ProfileAndPetIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"petpet_prof_{Guid.NewGuid():N}.db");
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
        var city = new City { Id = 1, Name = "臺北市", Code = "TPE" };
        var variety = new Variety { Id = 1, Name = "柴犬", KindCode = "Dog" };
        db.Cities.Add(city);
        db.Varieties.Add(variety);

        var alice = new Member
        {
            Email = "alice@profile.test",
            NormalizedEmail = "ALICE@PROFILE.TEST",
            PublicId = Guid.NewGuid(),
            Name = "愛麗絲",
            Phone = "0987654321",
            Bio = "熱愛狗狗的設計師",
            CityId = 1,
            Password = string.Empty,
            IsEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Version = Guid.NewGuid()
        };
        alice.Password = hasher.Hash(alice, "AlicePass123!");

        var bob = new Member
        {
            Email = "bob@profile.test",
            NormalizedEmail = "BOB@PROFILE.TEST",
            PublicId = Guid.NewGuid(),
            Name = "鮑伯",
            Password = string.Empty,
            IsEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Version = Guid.NewGuid()
        };
        bob.Password = hasher.Hash(bob, "BobPass123!");

        var publicPet = new Pet
        {
            OwnerEmail = "alice@profile.test",
            Name = "多多",
            KindCode = "Dog",
            VarietyId = 1,
            IsPublic = true,
            Version = Guid.NewGuid()
        };

        var privatePet = new Pet
        {
            OwnerEmail = "alice@profile.test",
            Name = "秘密小狗",
            KindCode = "Dog",
            VarietyId = 1,
            IsPublic = false,
            Version = Guid.NewGuid()
        };

        db.Members.AddRange(alice, bob);
        db.Pets.AddRange(publicPet, privatePet);
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
    [Trait("Requirement", "PP-MEM-03")]
    public async Task PublicProfile_ShowsOnlyWhitelistedFields_AndExcludesSensitive()
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        Guid alicePublicId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var alice = await db.Members.FirstAsync(m => m.Email == "alice@profile.test");
            alicePublicId = alice.PublicId;
        }

        var resp = await client.GetAsync($"/Members/{alicePublicId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains("愛麗絲", html);
        Assert.Contains("熱愛狗狗的設計師", html);
        Assert.Contains("臺北市", html);
        Assert.Contains("多多", html);

        // Sensitive fields MUST NOT appear
        Assert.DoesNotContain("alice@profile.test", html);
        Assert.DoesNotContain("0987654321", html);
        Assert.DoesNotContain("SecurityStamp", html);
        Assert.DoesNotContain("秘密小狗", html); // Private pet must not appear on public profile
    }

    [Fact]
    [Trait("Requirement", "PP-PET-02")]
    public async Task Pet_PrivatePet_HiddenFromGuestsAndOtherUsers_VisibleToOwner()
    {
        int privatePetId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var p = await db.Pets.FirstAsync(pet => pet.Name == "秘密小狗");
            privatePetId = p.Id;
        }

        // 1. Guest access returns 404
        using var guestClient = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var guestResp = await guestClient.GetAsync($"/Pet/Details/{privatePetId}");
        Assert.Equal(HttpStatusCode.NotFound, guestResp.StatusCode);

        // 2. Bob (other user) access returns 404
        var bobClient = await CreateAuthenticatedClientAsync("bob@profile.test", "BobPass123!");
        var bobResp = await bobClient.GetAsync($"/Pet/Details/{privatePetId}");
        Assert.Equal(HttpStatusCode.NotFound, bobResp.StatusCode);

        // 3. Alice (owner) access returns 200 OK
        var aliceClient = await CreateAuthenticatedClientAsync("alice@profile.test", "AlicePass123!");
        var aliceResp = await aliceClient.GetAsync($"/Pet/Details/{privatePetId}");
        Assert.Equal(HttpStatusCode.OK, aliceResp.StatusCode);

        var html = await aliceResp.Content.ReadAsStringAsync();
        Assert.Contains("秘密小狗", html);
        Assert.Contains("私人檔案", html);
    }

    [Fact]
    [Trait("Requirement", "PP-MEM-04")]
    public async Task PasswordChange_RevokesAllSessions_RequiresLogin()
    {
        var aliceClient = await CreateAuthenticatedClientAsync("alice@profile.test", "AlicePass123!");

        // Alice can access profile
        var getProfile = await aliceClient.GetAsync("/Account/Profile");
        Assert.Equal(HttpStatusCode.OK, getProfile.StatusCode);

        // Alice changes password
        var token = await ExtractAntiforgeryToken(aliceClient, "/Account/ChangePassword");
        var postChange = await aliceClient.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = "AlicePass123!",
            ["NewPassword"] = "NewAlicePass456!",
            ["ConfirmPassword"] = "NewAlicePass456!",
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.Redirect, postChange.StatusCode);
        Assert.Contains("/Member/Login", postChange.Headers.Location?.OriginalString);

        // Old session cannot access protected page anymore
        var nextReq = await aliceClient.GetAsync("/Account/Profile");
        Assert.Equal(HttpStatusCode.Redirect, nextReq.StatusCode);
        Assert.Contains("/Member/Login", nextReq.Headers.Location?.OriginalString);

        // Login with old password fails
        var loginToken = await ExtractAntiforgeryToken(aliceClient, "/Member/Login");
        var oldLogin = await aliceClient.PostAsync("/Member/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "alice@profile.test",
            ["password"] = "AlicePass123!",
            ["__RequestVerificationToken"] = loginToken
        }));
        Assert.Equal(HttpStatusCode.OK, oldLogin.StatusCode); // Returns login view with error

        // Login with new password succeeds
        var newLoginToken = await ExtractAntiforgeryToken(aliceClient, "/Member/Login");
        var newLogin = await aliceClient.PostAsync("/Member/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "alice@profile.test",
            ["password"] = "NewAlicePass456!",
            ["__RequestVerificationToken"] = newLoginToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, newLogin.StatusCode);
    }
}
