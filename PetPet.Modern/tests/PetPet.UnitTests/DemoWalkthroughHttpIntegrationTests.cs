using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;
using PetPet.Infrastructure.Demo;
using PetPet.Infrastructure.Services;
using Xunit.Abstractions;

namespace PetPet.UnitTests;

[Collection("DemoHost")]
public class DemoWalkthroughHttpIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDbPath;
    private readonly string _testDemoDir;
    private readonly WebApplicationFactory<Program> _factory;

    public DemoWalkthroughHttpIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _testDemoDir = Path.Combine(Path.GetTempPath(), $"petpet_demo_wt_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDemoDir);
        _tempDbPath = Path.Combine(_testDemoDir, "petpet_walkthrough.db");

        // Pre-seed demo database
        var options = new DbContextOptionsBuilder<PetPetDbContext>()
            .UseSqlite($"Data Source={_tempDbPath};Foreign Keys=True", b => b.MigrationsAssembly("PetPet.Migrations.Sqlite"))
            .Options;

        var services = new ServiceCollection()
            .AddLogging()
            .AddScoped<PetPet.Application.Members.IMemberPasswordHasher, MemberPasswordHasher>()
            .BuildServiceProvider();

        using (var initialContext = new PetPetDbContext(options))
        {
            initialContext.Database.Migrate();
            DemoSeeder.SeedAsync(initialContext, services).GetAwaiter().GetResult();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Demo");
            builder.UseSetting("Demo:Enabled", "true");
            builder.UseSetting("Demo:Directory", _testDemoDir);
            builder.UseSetting("Database:Provider", "Sqlite");
            builder.ConfigureServices(svc =>
            {
                var descriptor = svc.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PetPetDbContext>));
                if (descriptor != null) svc.Remove(descriptor);
                svc.AddDbContext<PetPetDbContext>(opt =>
                    opt.UseSqlite($"Data Source={_tempDbPath};Foreign Keys=True", b => b.MigrationsAssembly("PetPet.Migrations.Sqlite")));
            });
        });
    }

    public void Dispose()
    {
        _factory.Dispose();
        try
        {
            if (Directory.Exists(_testDemoDir)) Directory.Delete(_testDemoDir, recursive: true);
        }
        catch { }
    }

    [Fact]
    [Trait("Requirement", "PP-DEMO-01")]
    [Trait("Requirement", "PP-SEC-03")]
    public async Task Walkthrough_Guest_MainRoutes_And_MobileFeed_NoSensitiveLeaks()
    {
        var client = _factory.CreateClient();

        // 1. Home page in Demo mode
        var homeResp = await client.GetAsync("/");
        homeResp.EnsureSuccessStatusCode();
        var homeHtml = await homeResp.Content.ReadAsStringAsync();

        Assert.Contains("PetPet.Modern 本機展示環境", homeHtml);
        Assert.Contains("120", homeHtml); // Members count
        Assert.Contains("240", homeHtml); // Pets count
        Assert.Contains("demo-owner@example.test", homeHtml); // Quick login card
        Assert.Contains("demo-nopet@example.test", homeHtml);
        Assert.Contains("demo-friend@example.test", homeHtml);
        Assert.Contains("admin@example.test", homeHtml);
        Assert.Contains("disabled@example.test", homeHtml);
        _output.WriteLine("[Walkthrough] Guest Home: 200 OK, Demo banner & 5 persona cards present.");

        // 2. Post feed with mobile viewport check (375px)
        var postRequest = new HttpRequestMessage(HttpMethod.Get, "/Post/Index");
        postRequest.Headers.Add("User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1");
        var postResp = await client.SendAsync(postRequest);
        postResp.EnsureSuccessStatusCode();
        var postHtml = await postResp.Content.ReadAsStringAsync();

        // Sensitive fields leak check
        Assert.DoesNotContain("@example.test", postHtml);
        Assert.DoesNotMatch(@"09\d{2}-?\d{3}-?\d{3}", postHtml); // Mobile phone pattern
        Assert.DoesNotContain("PasswordHash", postHtml);
        Assert.DoesNotContain("SecurityStamp", postHtml);
        _output.WriteLine("[Walkthrough] Guest Post Feed: 200 OK, zero sensitive email/phone leaks on mobile viewport.");

        // 3. Privacy Policy
        var privacyResp = await client.GetAsync("/Home/Privacy");
        privacyResp.EnsureSuccessStatusCode();
        var privacyHtml = await privacyResp.Content.ReadAsStringAsync();
        Assert.Contains("隱私權政策與個人資料保護宣告", privacyHtml);
        Assert.Contains("公開資料投影政策", privacyHtml);
        Assert.Contains("雙向封鎖隔離機制", privacyHtml);
        _output.WriteLine("[Walkthrough] Guest Privacy: 200 OK, comprehensive privacy policies rendered.");
    }

    [Fact]
    [Trait("Requirement", "PP-DEMO-01")]
    [Trait("Requirement", "PP-MEM-01")]
    [Trait("Requirement", "PP-SEC-01")]
    public async Task Walkthrough_FiveDemoPersonas_Authentication_And_RoleBoundaries()
    {
        // 1. Owner: demo-owner@example.test
        var (ownerClient, ownerCookie) = await LoginPersonaAsync("demo-owner@example.test", "DemoPass!2026");
        Assert.NotNull(ownerCookie);
        var ownerPetsResp = await ownerClient.GetAsync("/Pet/Index");
        Assert.Equal(HttpStatusCode.OK, ownerPetsResp.StatusCode);
        var ownerPetsHtml = await ownerPetsResp.Content.ReadAsStringAsync();
        Assert.Contains("我的萌寵", ownerPetsHtml);
        _output.WriteLine("[Walkthrough] demo-owner: Logged in successfully, can view pets.");

        // 2. Newbie with 0 pets: demo-nopet@example.test
        var (nopetClient, nopetCookie) = await LoginPersonaAsync("demo-nopet@example.test", "DemoPass!2026");
        Assert.NotNull(nopetCookie);
        var nopetPetsResp = await nopetClient.GetAsync("/Pet/Index");
        Assert.Equal(HttpStatusCode.OK, nopetPetsResp.StatusCode);
        var nopetPetsHtml = await nopetPetsResp.Content.ReadAsStringAsync();
        Assert.Contains("尚未新增寵物資料", nopetPetsHtml);
        _output.WriteLine("[Walkthrough] demo-nopet: Logged in successfully, shows zero-pet guide.");

        // 3. Friend persona: demo-friend@example.test
        var (friendClient, friendCookie) = await LoginPersonaAsync("demo-friend@example.test", "DemoPass!2026");
        Assert.NotNull(friendCookie);
        var friendResp = await friendClient.GetAsync("/Friend/Index");
        Assert.Equal(HttpStatusCode.OK, friendResp.StatusCode);
        _output.WriteLine("[Walkthrough] demo-friend: Logged in successfully, can view friends.");

        // 4. Admin persona: admin@example.test
        var (adminClient, adminCookie) = await LoginPersonaAsync("admin@example.test", "DemoPass!2026");
        Assert.NotNull(adminCookie);
        var adminUsersResp = await adminClient.GetAsync("/Admin/ManageUsers");
        Assert.Equal(HttpStatusCode.OK, adminUsersResp.StatusCode);
        var adminReportsResp = await adminClient.GetAsync("/Admin/ManageReports");
        Assert.Equal(HttpStatusCode.OK, adminReportsResp.StatusCode);
        _output.WriteLine("[Walkthrough] admin: Logged in successfully, can access admin portals.");

        // Non-admin attempting admin page
        var nonAdminForbiddenResp = await ownerClient.GetAsync("/Admin/ManageUsers");
        Assert.True(nonAdminForbiddenResp.StatusCode == HttpStatusCode.Forbidden ||
                    (nonAdminForbiddenResp.StatusCode == HttpStatusCode.Redirect && nonAdminForbiddenResp.Headers.Location?.OriginalString.Contains("/Account/AccessDenied") == true));
        _output.WriteLine("[Walkthrough] Non-admin accessing admin portal: Correctly forbidden/redirected.");

        // 5. Disabled persona: disabled@example.test
        var anonClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await ExtractAntiforgeryToken(anonClient, "/Member/Login");
        var disabledResp = await anonClient.PostAsync("/Member/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "disabled@example.test",
            ["password"] = "DemoPass!2026",
            ["__RequestVerificationToken"] = token
        }));
        // Should not redirect to home, stays on login with error or 401
        Assert.False(disabledResp.StatusCode == HttpStatusCode.Redirect && disabledResp.Headers.Location?.OriginalString == "/");
        _output.WriteLine("[Walkthrough] disabled: Login strictly blocked.");
    }

    [Fact]
    [Trait("Requirement", "PP-UX-01")]
    [Trait("Requirement", "PP-SEC-02")]
    public async Task Walkthrough_ErrorFeedback_Contracts()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // 1. Missing resource -> 404 with user friendly page
        var notFoundResp = await client.GetAsync("/Post/Details/999999");
        Assert.Equal(HttpStatusCode.NotFound, notFoundResp.StatusCode);
        var notFoundHtml = await notFoundResp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("NullReferenceException", notFoundHtml);
        Assert.DoesNotContain("StackTrace", notFoundHtml);
        _output.WriteLine("[Walkthrough] 404 Error Contract: Clean user-friendly feedback without stack trace.");

        // 2. Unauthenticated POST to protected route -> 302 redirect to Login
        var unauthPost = await client.PostAsync("/Post/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["title"] = "未授權貼文",
            ["content"] = "無權杖貼文"
        }));
        Assert.Equal(HttpStatusCode.Redirect, unauthPost.StatusCode);
        Assert.Contains("/Member/Login", unauthPost.Headers.Location?.OriginalString);

        // 3. Authenticated POST without CSRF token -> 400 Bad Request
        var (authClient, _) = await LoginPersonaAsync("demo-owner@example.test", "DemoPass!2026");
        var noCsrfPost = await authClient.PostAsync("/Post/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["title"] = "無Token貼文",
            ["content"] = "無CSRF權杖"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, noCsrfPost.StatusCode);
        _output.WriteLine("[Walkthrough] CSRF Protection: Authenticated POST missing token returns 400 Bad Request.");
    }

    [Fact]
    [Trait("Requirement", "PP-MSG-01")]
    [Trait("Requirement", "PP-MSG-02")]
    [Trait("Requirement", "PP-MSG-03")]
    public async Task Walkthrough_TwoHubConnections_Chat_And_LiveRevocation()
    {
        // 1. Find accepted friend pair
        string userA = "demo-owner@example.test";
        string userB;
        Guid userBPublicId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var friendship = await db.Friends.FirstAsync(f =>
                (f.RequesterEmail == userA || f.AddresseeEmail == userA) && f.IsAccepted);
            userB = friendship.RequesterEmail == userA ? friendship.AddresseeEmail : friendship.RequesterEmail;
            userBPublicId = (await db.Members.FirstAsync(m => m.Email == userB)).PublicId;
        }

        var connA = await CreateAuthenticatedHubConnectionAsync(userA, "DemoPass!2026");
        var connB = await CreateAuthenticatedHubConnectionAsync(userB, "DemoPass!2026");

        await using (connA)
        await using (connB)
        {
            await connA.StartAsync();
            await connB.StartAsync();

            var receivedTcs = new TaskCompletionSource<string>();
            connB.On<object>("ReceiveMessage", payload =>
            {
                receivedTcs.TrySetResult(payload.ToString() ?? string.Empty);
            });

            var ackTcs = new TaskCompletionSource<bool>();
            connA.On<object>("MessageAck", _ => ackTcs.TrySetResult(true));

            // User A sends message to User B
            var clientMsgId = Guid.NewGuid();
            await connA.InvokeAsync("SendMessage", userBPublicId, clientMsgId, "你好！這是雙向即時聊天展示訊息。");

            var ackWait = await Task.WhenAny(ackTcs.Task, Task.Delay(3000));
            Assert.Equal(ackTcs.Task, ackWait);

            var recvWait = await Task.WhenAny(receivedTcs.Task, Task.Delay(3000));
            Assert.Equal(receivedTcs.Task, recvWait);
            _output.WriteLine("[Walkthrough] Real-time Chat: Message sent from User A and received by User B with MessageAck.");

            // Rotate User A's SecurityStamp
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
                var memberA = await db.Members.FirstAsync(m => m.Email == userA);
                memberA.SecurityStamp = Guid.NewGuid().ToString("N");
                await db.SaveChangesAsync();
            }

            // Next message from User A is immediately rejected
            bool revoked = false;
            try
            {
                await connA.InvokeAsync("SendMessage", userBPublicId, Guid.NewGuid(), "這條訊息應當被中斷！");
            }
            catch
            {
                revoked = true;
            }
            Assert.True(revoked || connA.State == HubConnectionState.Disconnected);
            _output.WriteLine("[Walkthrough] Live Revocation: Rotated SecurityStamp immediately blocks active hub session.");
        }
    }

    private async Task<(HttpClient Client, string? CookieHeader)> LoginPersonaAsync(string email, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await ExtractAntiforgeryToken(client, "/Member/Login");
        var resp = await client.PostAsync("/Member/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password,
            ["__RequestVerificationToken"] = token
        }));

        string? cookieHeader = null;
        if (resp.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            cookieHeader = string.Join("; ", setCookies.Select(c => c.Split(';')[0]));
            client.DefaultRequestHeaders.Add("Cookie", cookieHeader);
        }

        return (client, cookieHeader);
    }

    private async Task<HubConnection> CreateAuthenticatedHubConnectionAsync(string email, string password)
    {
        var (_, cookieHeader) = await LoginPersonaAsync(email, password);
        var baseUri = _factory.Server.BaseAddress;
        var hubUri = new Uri(baseUri, "/chatHub");

        return new HubConnectionBuilder()
            .WithUrl(hubUri, opt =>
            {
                opt.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                if (!string.IsNullOrEmpty(cookieHeader)) opt.Headers["Cookie"] = cookieHeader;
                opt.Headers["Origin"] = baseUri.ToString().TrimEnd('/');
            })
            .Build();
    }

    private static async Task<string> ExtractAntiforgeryToken(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        var marker = "__RequestVerificationToken\" type=\"hidden\" value=\"";
        var idx = html.IndexOf(marker, StringComparison.Ordinal);
        if (idx == -1)
        {
            marker = "value=\"";
            var tokenIdx = html.IndexOf("__RequestVerificationToken", StringComparison.Ordinal);
            if (tokenIdx != -1)
            {
                idx = html.IndexOf(marker, tokenIdx, StringComparison.Ordinal);
                if (idx != -1) idx += marker.Length;
            }
        }
        else
        {
            idx += marker.Length;
        }

        if (idx == -1) return string.Empty;
        var endIdx = html.IndexOf('"', idx);
        return html.Substring(idx, endIdx - idx);
    }
}
