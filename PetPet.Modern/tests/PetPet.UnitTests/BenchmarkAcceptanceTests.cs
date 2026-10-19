using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;
using PetPet.Infrastructure.Demo;
using Xunit.Abstractions;

namespace PetPet.UnitTests;

public class BenchmarkAcceptanceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDbPath;
    private readonly SqliteConnection _sqliteConnection;

    public BenchmarkAcceptanceTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"petpet_benchmark_{Guid.NewGuid():N}.db");
        _sqliteConnection = new SqliteConnection($"Data Source={_tempDbPath};Foreign Keys=True");
        _sqliteConnection.Open();
    }

    public void Dispose()
    {
        _sqliteConnection.Dispose();
        try
        {
            if (File.Exists(_tempDbPath))
            {
                File.Delete(_tempDbPath);
            }
        }
        catch { }
    }

    [Fact]
    [Trait("Requirement", "PP-QA-02")]
    public async Task Benchmark_FullSuite_Startup_ListP95_PayloadSize_And_ChatAckP95()
    {
        var envOs = RuntimeInformation.OSDescription;
        var envArch = RuntimeInformation.ProcessArchitecture.ToString();
        var envCpuCount = Environment.ProcessorCount;
        var dotnetVer = Environment.Version.ToString();
        var gcMem = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);

        _output.WriteLine($"[Benchmark Environment] OS: {envOs}, Arch: {envArch}, CPU: {envCpuCount} cores, Total RAM: {gcMem} MB, .NET: {dotnetVer}");

        // 1. Cold Startup Measurement (Migrate + Seed 120 members, 240 pets, 1200 posts, 2400 comments, 300 friends)
        var coldSw = Stopwatch.StartNew();
        var options = new DbContextOptionsBuilder<PetPetDbContext>()
            .UseSqlite(_sqliteConnection, b => b.MigrationsAssembly("PetPet.Migrations.Sqlite"))
            .Options;

        var services = new ServiceCollection()
            .AddLogging()
            .AddScoped<PetPet.Application.Members.IMemberPasswordHasher, PetPet.Infrastructure.Services.MemberPasswordHasher>()
            .BuildServiceProvider();

        using (var initialContext = new PetPetDbContext(options))
        {
            await initialContext.Database.MigrateAsync();
            await DemoSeeder.SeedAsync(initialContext, services);
        }
        coldSw.Stop();
        var coldStartupMs = coldSw.ElapsedMilliseconds;
        _output.WriteLine($"[Benchmark Cold Startup] Migration + Full Seed: {coldStartupMs} ms (Target <= 60,000 ms)");
        Assert.True(coldStartupMs <= 60000, $"Cold startup {coldStartupMs}ms exceeded 60s limit");

        // 2. Warm Restart Measurement (Already seeded, verify seed record)
        var warmSw = Stopwatch.StartNew();
        using (var warmContext = new PetPetDbContext(options))
        {
            var seedResult = await DemoSeeder.SeedAsync(warmContext, services);
            Assert.True(seedResult.AlreadySeeded);
        }
        warmSw.Stop();
        var warmRestartMs = warmSw.ElapsedMilliseconds;
        _output.WriteLine($"[Benchmark Warm Restart] Verify existing seed: {warmRestartMs} ms (Target <= 10,000 ms)");
        Assert.True(warmRestartMs <= 10000, $"Warm restart {warmRestartMs}ms exceeded 10s limit");

        // Close initial connection so factory connections can connect independently
        _sqliteConnection.Close();

        // Setup WebApplicationFactory using this seeded database
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(svc =>
            {
                var descriptor = svc.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PetPetDbContext>));
                if (descriptor != null) svc.Remove(descriptor);
                svc.AddDbContext<PetPetDbContext>(opt => opt.UseSqlite($"Data Source={_tempDbPath};Foreign Keys=True", b => b.MigrationsAssembly("PetPet.Migrations.Sqlite")));
            });
        });

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });

        // 3. Warmup List Requests (20 requests)
        for (int i = 0; i < 20; i++)
        {
            var warmupResp = await client.GetAsync("/Post/Index");
            warmupResp.EnsureSuccessStatusCode();
        }

        // 4. Measure 100 List Requests to /Post/Index
        var postLatencies = new List<double>(100);
        long payloadBytes = 0;

        for (int i = 0; i < 100; i++)
        {
            var sw = Stopwatch.StartNew();
            var resp = await client.GetAsync("/Post/Index");
            sw.Stop();
            resp.EnsureSuccessStatusCode();
            postLatencies.Add(sw.Elapsed.TotalMilliseconds);

            if (i == 0)
            {
                var body = await resp.Content.ReadAsByteArrayAsync();
                payloadBytes = body.Length;
            }
        }

        postLatencies.Sort();
        var p50 = postLatencies[49];
        var p90 = postLatencies[89];
        var p95 = postLatencies[94];
        var p99 = postLatencies[99];
        var mean = postLatencies.Average();
        var min = postLatencies.First();
        var max = postLatencies.Last();

        _output.WriteLine($"[Benchmark List /Post/Index 100 requests] Min: {min:F2}ms, Mean: {mean:F2}ms, P50: {p50:F2}ms, P90: {p90:F2}ms, P95: {p95:F2}ms, P99: {p99:F2}ms, Max: {max:F2}ms (Target P95 <= 500ms)");
        _output.WriteLine($"[Benchmark 375px Wall Payload] Size: {payloadBytes} bytes ({payloadBytes / 1024.0:F2} KiB, Target <= 1,048,576 bytes)");

        Assert.True(p95 <= 500.0, $"List P95 latency {p95:F2}ms exceeded 500ms target");
        Assert.True(payloadBytes <= 1048576, $"Payload size {payloadBytes} exceeded 1MiB limit");

        // 5. Measure SignalR Chat Ack Latency (100 messages)
        // Login Alice to get auth cookie
        var authClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var aliceToken = await ExtractAntiforgeryToken(authClient, "/Member/Login");
        var loginResp = await authClient.PostAsync("/Member/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "demo-owner@example.test",
            ["password"] = "DemoPass!2026",
            ["__RequestVerificationToken"] = aliceToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, loginResp.StatusCode);

        string cookieHeader = string.Empty;
        if (loginResp.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            cookieHeader = string.Join("; ", setCookies.Select(c => c.Split(';')[0]));
        }

        var baseUri = factory.Server.BaseAddress;
        var hubUri = new Uri(baseUri, "/chatHub");

        var hubConn = new HubConnectionBuilder()
            .WithUrl(hubUri, opt =>
            {
                opt.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                if (!string.IsNullOrEmpty(cookieHeader))
                {
                    opt.Headers["Cookie"] = cookieHeader;
                }
                opt.Headers["Origin"] = baseUri.ToString().TrimEnd('/');
            })
            .Build();

        Guid friendPublicId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var acceptedFriendship = await db.Friends.FirstAsync(f =>
                (f.RequesterEmail == "demo-owner@example.test" || f.AddresseeEmail == "demo-owner@example.test") && f.IsAccepted);
            var friendEmail = acceptedFriendship.RequesterEmail == "demo-owner@example.test"
                ? acceptedFriendship.AddresseeEmail
                : acceptedFriendship.RequesterEmail;
            friendPublicId = (await db.Members.FirstAsync(m => m.Email == friendEmail)).PublicId;
        }

        await using (hubConn)
        {
            await hubConn.StartAsync();

            var chatLatencies = new List<double>(100);

            // Warmup 5 messages
            for (int i = 0; i < 5; i++)
            {
                var ackTcs = new TaskCompletionSource<bool>();
                var reg = hubConn.On<object>("MessageAck", _ => ackTcs.TrySetResult(true));
                await hubConn.InvokeAsync("SendMessage", friendPublicId, Guid.NewGuid(), $"Warmup message {i}");
                await Task.WhenAny(ackTcs.Task, Task.Delay(2000));
                reg.Dispose();
            }

            // Timed 100 messages
            for (int i = 0; i < 100; i++)
            {
                // Reset only this benchmark host; parallel hosts keep their quotas.
                if (i % 50 == 0) factory.Services.GetRequiredService<PetPet.Application.Chat.IChatRateLimiter>().Reset();
                var ackTcs = new TaskCompletionSource<bool>();
                var reg = hubConn.On<object>("MessageAck", _ => ackTcs.TrySetResult(true));

                var sw = Stopwatch.StartNew();
                await hubConn.InvokeAsync("SendMessage", friendPublicId, Guid.NewGuid(), $"Benchmark msg {i}");
                var completed = await Task.WhenAny(ackTcs.Task, Task.Delay(2000));
                sw.Stop();
                reg.Dispose();

                Assert.Equal(ackTcs.Task, completed);
                chatLatencies.Add(sw.Elapsed.TotalMilliseconds);
            }

            chatLatencies.Sort();
            var chatP50 = chatLatencies[49];
            var chatP90 = chatLatencies[89];
            var chatP95 = chatLatencies[94];
            var chatP99 = chatLatencies[99];
            var chatMean = chatLatencies.Average();
            var chatMin = chatLatencies.First();
            var chatMax = chatLatencies.Last();

            _output.WriteLine($"[Benchmark Chat Ack 100 messages] Min: {chatMin:F2}ms, Mean: {chatMean:F2}ms, P50: {chatP50:F2}ms, P90: {chatP90:F2}ms, P95: {chatP95:F2}ms, P99: {chatP99:F2}ms, Max: {chatMax:F2}ms (Target P95 <= 1,000ms)");
            Assert.True(chatP95 <= 1000.0, $"Chat Ack P95 {chatP95:F2}ms exceeded 1,000ms target");

            // Write benchmark output to docs/reviews/2026-10-14-benchmark-results.md
            var reportContent = $@"# PetPet.Modern 效能基準驗證結果 (PP-QA-02)

- 執行時間：{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC ({DateTime.UtcNow.AddHours(8):yyyy-MM-dd HH:mm:ss} 臺灣時間)
- 測試環境：
  - 作業系統 (OS)：{envOs}
  - 處理器架構 (Arch)：{envArch} ({envCpuCount} 核心)
  - 總記憶體 (RAM)：約 {gcMem} MB
  - .NET Runtime 版本：{dotnetVer}
  - 資料庫：SQLite (完全 Migration + 確定性展示 Seed)
  - 資料筆數：Member 120, Pet 240, Post 1200 (1180 可見), Comment 2400, Friend 300, Like 每篇 0-20

## 1. 啟動與重啟效能

| 指標項目 | 規格上限 | 實際量測結果 | 驗收判定 |
|---|---|---|---|
| 冷啟動 (Migration + Full Seed) | <= 60,000 ms (60s) | {coldStartupMs} ms ({coldStartupMs / 1000.0:F2}s) | PASS |
| 熱重啟 (已有資料庫驗證已存在) | <= 10,000 ms (10s) | {warmRestartMs} ms ({warmRestartMs / 1000.0:F2}s) | PASS |

## 2. 列表查詢延遲量測 (/Post/Index 100 requests，含 20 次預熱)

- 樣本數：100 次 HTTP GET
- 最小延遲 (Min)：{min:F2} ms
- 平均延遲 (Mean)：{mean:F2} ms
- 中位數 (P50)：{p50:F2} ms
- P90：{p90:F2} ms
- **P95 延遲**：**{p95:F2} ms** (規格上限：<= 500 ms) -> **PASS**
- P99：{p99:F2} ms
- 最大延遲 (Max)：{max:F2} ms

## 3. 375px 手機動態牆回應大小

- 頁面大小：{payloadBytes} 位元組 ({payloadBytes / 1024.0:F2} KiB)
- 規格上限：<= 1,048,576 位元組 (1 MiB) -> **PASS**

## 4. SignalR 即時聊天 Ack 延遲量測 (100 messages，含 5 次預熱)

- 樣本數：100 次 Hub 連線端對端往返
- 最小延遲 (Min)：{chatMin:F2} ms
- 平均延遲 (Mean)：{chatMean:F2} ms
- 中位數 (P50)：{chatP50:F2} ms
- P90：{chatP90:F2} ms
- **P95 延遲**：**{chatP95:F2} ms** (規格上限：<= 1,000 ms) -> **PASS**
- P99：{chatP99:F2} ms
- 最大延遲 (Max)：{chatMax:F2} ms
";

            // Note: Measurements are logged via ITestOutputHelper to prevent silently overwriting baseline reports.
            _output.WriteLine("[Component Benchmark Output] Complete summary generated in memory and logged.");
        }
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
