using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPet.Infrastructure.Data;

namespace PetPet.UnitTests;

[Collection("DemoHost")]
public class DemoWebHostTests : IDisposable
{
    private readonly string _testDemoDir;

    public DemoWebHostTests()
    {
        _testDemoDir = Path.Combine(Path.GetTempPath(), $"petpet_demo_web_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDemoDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDemoDir))
            {
                Directory.Delete(_testDemoDir, recursive: true);
            }
        }
        catch {}
    }

    private sealed class DemoTestApp(string demoDir) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Demo");
            builder.UseSetting("Demo:Enabled", "true");
            builder.UseSetting("Demo:Directory", demoDir);
            builder.UseSetting("Database:Provider", "Sqlite");

            var dbPath = Path.Combine(demoDir, "petpet-demo.db");
            builder.ConfigureServices(services =>
            {
                // Ensure db context points to isolated test db
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PetPetDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<PetPetDbContext>(options =>
                    options.UseSqlite($"Data Source={dbPath}", b => b.MigrationsAssembly("PetPet.Migrations.Sqlite")));
            });
        }
    }

    [Fact]
    [Trait("Requirement", "PP-RUN-02")]
    [Trait("Requirement", "PP-RUN-04")]
    public async Task DemoHost_ReadinessAndLiveness_Return200()
    {
        using var app = new DemoTestApp(_testDemoDir);
        using var client = app.CreateClient();

        // 1. Check live endpoint
        var liveResp = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, liveResp.StatusCode);
        Assert.Equal("live", await liveResp.Content.ReadAsStringAsync());

        // 2. Check ready endpoint
        var readyResp = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, readyResp.StatusCode);
        Assert.Equal("ready", await readyResp.Content.ReadAsStringAsync());

        // 3. Check Home page loads
        var homeResp = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, homeResp.StatusCode);

        // 4. Verify seeded counts in the isolated DB
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
        Assert.Equal(120, await db.Members.CountAsync());
        Assert.Equal(240, await db.Pets.CountAsync());
        Assert.Equal(1200, await db.Posts.CountAsync());
        Assert.Equal(2400, await db.Comments.CountAsync());
        Assert.Equal(300, await db.Friends.CountAsync());
        Assert.True(await db.Likes.AnyAsync());
    }
}
