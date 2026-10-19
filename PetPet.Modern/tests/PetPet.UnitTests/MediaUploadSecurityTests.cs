using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PetPet.Application.Media;
using PetPet.Application.Members;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;
using SkiaSharp;

namespace PetPet.UnitTests;

[Trait("Requirement", "PP-MEDIA-02")]
public class MediaUploadSecurityTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _sqliteConnection;
    private readonly WebApplicationFactory<Program> _factory;

    public MediaUploadSecurityTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"petpet_media_{Guid.NewGuid():N}.db");
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
        SeedUsers(db, scope.ServiceProvider.GetRequiredService<IMemberPasswordHasher>());
    }

    private void SeedUsers(PetPetDbContext db, IMemberPasswordHasher hasher)
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
        alice.Password = hasher.Hash(alice, "Password123!");

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
        bob.Password = hasher.Hash(bob, "Password123!");

        db.Members.AddRange(alice, bob);
        db.SaveChanges();
    }

    private static async Task<string> ExtractAntiforgeryToken(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private async Task<(HttpClient client, string csrfToken)> CreateAuthenticatedClientAsync(string email, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var loginToken = await ExtractAntiforgeryToken(client, "/Member/Login");

        var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Email", email },
            { "Password", password },
            { "__RequestVerificationToken", loginToken }
        });

        var postLogin = await client.PostAsync("/Member/Login", loginContent);
        Assert.Equal(HttpStatusCode.Redirect, postLogin.StatusCode);

        var authCsrf = await ExtractAntiforgeryToken(client, "/Post/Index");
        return (client, authCsrf);
    }

    private static byte[] GenerateTestImageBytes(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Blue);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    [Fact]
    public async Task Media_Unauthenticated_Upload_Rejected()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "file", "test.jpg");

        var res = await client.PostAsync("/Image/Upload", content);
        Assert.True(res.StatusCode == HttpStatusCode.Redirect || res.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Media_Valid_Image_Upload_SavesToAppUploads_AndCreatesDbRecord()
    {
        var (aliceClient, aliceCsrf) = await CreateAuthenticatedClientAsync("alice@petpet.test", "Password123!");

        byte[] pngBytes = GenerateTestImageBytes(200, 150, SKEncodedImageFormat.Png);

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "puppy.png");
        content.Add(new StringContent(aliceCsrf), "__RequestVerificationToken");

        var req = new HttpRequestMessage(HttpMethod.Post, "/Image/Upload")
        {
            Content = content
        };
        req.Headers.Add("X-CSRF-TOKEN", aliceCsrf);

        var res = await aliceClient.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var json = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var idStr = json.GetProperty("id").GetString();
        var urlStr = json.GetProperty("url").GetString();
        Assert.NotNull(idStr);
        Assert.NotNull(urlStr);

        var mediaId = Guid.Parse(idStr);
        Assert.Equal($"/Media/{mediaId}", urlStr);

        // Verify in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var media = await db.Media.FirstOrDefaultAsync(m => m.Id == mediaId);
            Assert.NotNull(media);
            Assert.Equal("alice@petpet.test", media.OwnerEmail);
            Assert.Equal("image/png", media.ContentType);
            Assert.Equal(200, media.Width);
            Assert.Equal(150, media.Height);
            Assert.True(media.ByteLength > 0);
        }
    }

    [Fact]
    public async Task Media_DisallowedFormats_SVG_GIF_ArbitraryFile_Rejected_400()
    {
        var (aliceClient, aliceCsrf) = await CreateAuthenticatedClientAsync("alice@petpet.test", "Password123!");

        // 1. SVG
        var svgBytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><circle r=\"10\"/></svg>");
        var contentSvg = new MultipartFormDataContent();
        contentSvg.Add(new ByteArrayContent(svgBytes), "file", "malicious.svg");
        contentSvg.Add(new StringContent(aliceCsrf), "__RequestVerificationToken");

        var reqSvg = new HttpRequestMessage(HttpMethod.Post, "/Image/Upload") { Content = contentSvg };
        reqSvg.Headers.Add("X-CSRF-TOKEN", aliceCsrf);
        var resSvg = await aliceClient.SendAsync(reqSvg);
        Assert.Equal(HttpStatusCode.BadRequest, resSvg.StatusCode);

        // 2. GIF
        var gifBytes = Encoding.ASCII.GetBytes("GIF89a\x01\x00\x01\x00\x80\x00\x00\x00\x00\x00\xff\xff\xff!\xf9\x04\x01\x00\x00\x00\x00,\x00\x00\x00\x00\x01\x00\x01\x00\x00\x02\x02D\x01\x00;");
        var contentGif = new MultipartFormDataContent();
        contentGif.Add(new ByteArrayContent(gifBytes), "file", "anim.gif");
        contentGif.Add(new StringContent(aliceCsrf), "__RequestVerificationToken");

        var reqGif = new HttpRequestMessage(HttpMethod.Post, "/Image/Upload") { Content = contentGif };
        reqGif.Headers.Add("X-CSRF-TOKEN", aliceCsrf);
        var resGif = await aliceClient.SendAsync(reqGif);
        Assert.Equal(HttpStatusCode.BadRequest, resGif.StatusCode);

        // 3. Plain text / exe
        var txtBytes = Encoding.UTF8.GetBytes("echo 'Hello World'");
        var contentTxt = new MultipartFormDataContent();
        contentTxt.Add(new ByteArrayContent(txtBytes), "file", "script.sh");
        contentTxt.Add(new StringContent(aliceCsrf), "__RequestVerificationToken");

        var reqTxt = new HttpRequestMessage(HttpMethod.Post, "/Image/Upload") { Content = contentTxt };
        reqTxt.Headers.Add("X-CSRF-TOKEN", aliceCsrf);
        var resTxt = await aliceClient.SendAsync(reqTxt);
        Assert.Equal(HttpStatusCode.BadRequest, resTxt.StatusCode);
    }

    [Fact]
    public async Task Media_Serving_ScopeCheck_And_NosniffHeader()
    {
        var (aliceClient, aliceCsrf) = await CreateAuthenticatedClientAsync("alice@petpet.test", "Password123!");
        var (bobClient, _) = await CreateAuthenticatedClientAsync("bob@petpet.test", "Password123!");

        // Alice uploads image
        byte[] jpegBytes = GenerateTestImageBytes(120, 120, SKEncodedImageFormat.Jpeg);
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(jpegBytes), "file", "alice.jpg");
        content.Add(new StringContent(aliceCsrf), "__RequestVerificationToken");

        var uploadReq = new HttpRequestMessage(HttpMethod.Post, "/Image/Upload") { Content = content };
        uploadReq.Headers.Add("X-CSRF-TOKEN", aliceCsrf);
        var uploadRes = await aliceClient.SendAsync(uploadReq);
        Assert.Equal(HttpStatusCode.OK, uploadRes.StatusCode);

        var json = await uploadRes.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var mediaId = Guid.Parse(json.GetProperty("id").GetString()!);

        // 1. Alice (owner) can access -> 200 OK with X-Content-Type-Options: nosniff
        var aliceGet = await aliceClient.GetAsync($"/Media/{mediaId}");
        Assert.Equal(HttpStatusCode.OK, aliceGet.StatusCode);
        Assert.True(aliceGet.Headers.Contains("X-Content-Type-Options"));
        Assert.Equal("nosniff", aliceGet.Headers.GetValues("X-Content-Type-Options").First());

        // 2. Bob (non-owner) accesses unreferenced image -> 404 NotFound
        var bobGet = await bobClient.GetAsync($"/Media/{mediaId}");
        Assert.Equal(HttpStatusCode.NotFound, bobGet.StatusCode);

        // 3. Alice associates media with her public post
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            db.Posts.Add(new Post
            {
                AuthorEmail = "alice@petpet.test",
                Title = "我的可愛貼文",
                Content = "請看這張照片！",
                ImageUrl = $"/Media/{mediaId}",
                IsEnabled = true,
                IsDeleted = false
            });
            await db.SaveChangesAsync();
        }

        // 4. Now that post is public, Bob can access -> 200 OK
        var bobGetAfterPublic = await bobClient.GetAsync($"/Media/{mediaId}");
        Assert.Equal(HttpStatusCode.OK, bobGetAfterPublic.StatusCode);
        Assert.Equal("nosniff", bobGetAfterPublic.Headers.GetValues("X-Content-Type-Options").First());
    }

    [Fact]
    public async Task Media_OrphanCleanup_RemovesUnreferencedOldFiles()
    {
        Guid orphanId;
        Guid keptId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();

            byte[] b1 = GenerateTestImageBytes(50, 50, SKEncodedImageFormat.Png);
            using var s1 = new MemoryStream(b1);
            var res1 = await mediaService.UploadImageAsync(s1, "image/png", b1.Length, "alice@petpet.test");
            orphanId = res1.Id;

            byte[] b2 = GenerateTestImageBytes(50, 50, SKEncodedImageFormat.Png);
            using var s2 = new MemoryStream(b2);
            var res2 = await mediaService.UploadImageAsync(s2, "image/png", b2.Length, "alice@petpet.test");
            keptId = res2.Id;

            // Associate keptId with a member avatar
            var alice = await db.Members.FirstAsync(m => m.Email == "alice@petpet.test");
            alice.Photo = res2.Url;

            // Age both records past 25 hours
            var oldMedia1 = await db.Media.FirstAsync(m => m.Id == orphanId);
            oldMedia1.CreatedAt = DateTime.UtcNow.AddHours(-25);

            var oldMedia2 = await db.Media.FirstAsync(m => m.Id == keptId);
            oldMedia2.CreatedAt = DateTime.UtcNow.AddHours(-25);

            await db.SaveChangesAsync();
        }

        // Run cleanup
        using (var scope = _factory.Services.CreateScope())
        {
            var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();
            int cleaned = await mediaService.CleanupOrphansAsync(TimeSpan.FromHours(24));
            Assert.Equal(1, cleaned); // Only orphanId should be cleaned
        }

        // Verify DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            Assert.Null(await db.Media.FirstOrDefaultAsync(m => m.Id == orphanId));
            Assert.NotNull(await db.Media.FirstOrDefaultAsync(m => m.Id == keptId));
        }
    }

    public void Dispose()
    {
        _factory.Dispose();
        _sqliteConnection.Dispose();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }
}
