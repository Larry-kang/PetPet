using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PetPet.Application.Chat;
using PetPet.Application.Media;
using PetPet.Application.Members;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;
using PetPet.Infrastructure.Demo;
using PetPet.Infrastructure.Services;
using PetPet.Web.Hubs;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

// Environment & Configuration Validation
var isDemo = builder.Environment.IsEnvironment("Demo");
var provider = builder.Configuration["Database:Provider"] ?? (isDemo ? "Sqlite" : "SqlServer");

if (!string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase) &&
    !string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException($"不支援的資料庫提供者：'{provider}'。僅支援 'Sqlite' 與 'SqlServer'。");
}

if (isDemo)
{
    var demoEnabled = builder.Configuration.GetValue<bool>("Demo:Enabled");
    if (!demoEnabled || !string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Demo 模式設定衝突：必須同時滿足 Demo:Enabled=true 且 Database:Provider=Sqlite。");
    }

    var urls = builder.Configuration["ASPNETCORE_URLS"] ?? builder.Configuration["urls"];
    if (!string.IsNullOrEmpty(urls))
    {
        var urlList = urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var u in urlList)
        {
            if (Uri.TryCreate(u, UriKind.Absolute, out var uri))
            {
                if (uri.Host != "127.0.0.1" && uri.Host != "localhost" && uri.Host != "::1")
                {
                    throw new InvalidOperationException($"Demo 模式僅允許綁定 loopback (127.0.0.1 / localhost)，禁止公開監聽網址: {u}");
                }
            }
            else if (u.Contains("0.0.0.0") || u.Contains("*") || u.Contains("+"))
            {
                throw new InvalidOperationException($"Demo 模式僅允許綁定 loopback (127.0.0.1 / localhost)，禁止萬用或公開監聽網址: {u}");
            }
        }
    }
}

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));
builder.Services.AddScoped<IMemberRepository, MemberRepository>();
builder.Services.AddScoped<IMemberPasswordHasher, MemberPasswordHasher>();
builder.Services.AddScoped<MemberAccountService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddSingleton<IChatRateLimiter, ChatRateLimiter>();

// Configure Database
string demoDir = builder.Configuration["Demo:Directory"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "demo");
string demoDbPath = Path.Combine(demoDir, "petpet-demo.db");
FileStream? runLockHandle = null;

if (isDemo)
{
    // Acquire exclusive lifetime run lock
    runLockHandle = DemoLockManager.AcquireRunLock(demoDir);
    var sqliteConn = $"Data Source={demoDbPath}";
    builder.Services.AddDbContext<PetPetDbContext>(options =>
        options.UseSqlite(sqliteConn, b => b.MigrationsAssembly("PetPet.Migrations.Sqlite")));
}
else if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
{
    var sqliteConn = builder.Configuration.GetConnectionString("SqliteConnection");
    if (string.IsNullOrWhiteSpace(sqliteConn))
    {
        throw new InvalidOperationException("非 Demo 模式使用 Sqlite 時，必須明確設定 ConnectionStrings:SqliteConnection，不得回退至 Demo 資料庫。");
    }
    builder.Services.AddDbContext<PetPetDbContext>(options =>
        options.UseSqlite(sqliteConn, b => b.MigrationsAssembly("PetPet.Migrations.Sqlite")));
}
else
{
    var sqlConn = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContext<PetPetDbContext>(options =>
        options.UseSqlServer(sqlConn, b => b.MigrationsAssembly("PetPet.Migrations.SqlServer")));
}

// SignalR
builder.Services.AddSignalR();

// Rate Limiting (PP-SEC-04: login IP 10/min + identity 5/min; register IP 5/min; content 30/min; friend 10/min; report 10/min; upload 5/min)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds))).ToString();
        }
        else
        {
            context.HttpContext.Response.Headers.RetryAfter = "60";
        }
        await context.HttpContext.Response.WriteAsync("請求過於頻繁，請稍後再試 (429 Too Many Requests)。", token);
    };

    // 1. Login policy: IP 10/min chained with normalized identity 5/min
    var ipLimiters = new System.Collections.Concurrent.ConcurrentDictionary<string, FixedWindowRateLimiter>();
    var identityLimiters = new System.Collections.Concurrent.ConcurrentDictionary<string, FixedWindowRateLimiter>();

    options.AddPolicy("login-limit", ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown_ip";
        string identityKey = "unknown_identity";
        if (ctx.Request.HasFormContentType && ctx.Request.Form.TryGetValue("email", out var formEmail) && !string.IsNullOrWhiteSpace(formEmail))
        {
            using var sha = SHA256.Create();
            var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(formEmail.ToString().Trim().ToUpperInvariant())));
            identityKey = hash.Substring(0, 16);
        }
        else if (ctx.Request.Headers.TryGetValue("X-Login-Identity", out var headerId) && !string.IsNullOrWhiteSpace(headerId))
        {
            using var sha = SHA256.Create();
            var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(headerId.ToString().Trim().ToUpperInvariant())));
            identityKey = hash.Substring(0, 16);
        }

        var ipLimiter = ipLimiters.GetOrAdd(ip, _ => new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));

        var idLimiter = identityLimiters.GetOrAdd(identityKey, _ => new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));

        return RateLimitPartition.Get($"{ip}_{identityKey}", _ => new ChainedRateLimiter(ipLimiter, idLimiter));
    });

    // 2. Register policy: IP 5/min
    options.AddPolicy("register-limit", ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown_ip";
        return RateLimitPartition.GetFixedWindowLimiter($"register_ip_{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    // 3. Content write policy (Post / Comment): Member 30/min
    options.AddPolicy("content-limit", ctx =>
    {
        var memberKey = ctx.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? ctx.User?.FindFirst(ClaimTypes.Email)?.Value
                     ?? ctx.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown_member";
        return RateLimitPartition.GetFixedWindowLimiter($"content_member_{memberKey}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    // 4. Friend invitation policy: Member 10/min
    options.AddPolicy("friend-limit", ctx =>
    {
        var memberKey = ctx.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? ctx.User?.FindFirst(ClaimTypes.Email)?.Value
                     ?? ctx.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown_member";
        return RateLimitPartition.GetFixedWindowLimiter($"friend_member_{memberKey}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    // 5. Report submission policy: Member 10/min
    options.AddPolicy("report-limit", ctx =>
    {
        var memberKey = ctx.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? ctx.User?.FindFirst(ClaimTypes.Email)?.Value
                     ?? ctx.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown_member";
        return RateLimitPartition.GetFixedWindowLimiter($"report_member_{memberKey}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    // 6. Media upload policy: Member 5/min
    options.AddPolicy("upload-limit", ctx =>
    {
        var memberKey = ctx.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? ctx.User?.FindFirst(ClaimTypes.Email)?.Value
                     ?? ctx.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown_member";
        return RateLimitPartition.GetFixedWindowLimiter($"upload_member_{memberKey}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

// Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Member/Login";
        options.LogoutPath = "/Member/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<PetPetDbContext>();
                var email = context.Principal?.FindFirst(ClaimTypes.Email)?.Value;
                var stamp = context.Principal?.FindFirst("SecurityStamp")?.Value;
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(stamp))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync();
                    return;
                }

                // 8-hour absolute session check
                var issuedUtcClaim = context.Principal?.FindFirst("IssuedUtc")?.Value;
                if (long.TryParse(issuedUtcClaim, out var issuedSeconds))
                {
                    var issuedTime = DateTimeOffset.FromUnixTimeSeconds(issuedSeconds);
                    if (DateTimeOffset.UtcNow - issuedTime > TimeSpan.FromHours(8))
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync();
                        return;
                    }
                }
                else if (context.Properties.IssuedUtc.HasValue &&
                         DateTimeOffset.UtcNow - context.Properties.IssuedUtc.Value > TimeSpan.FromHours(8))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync();
                    return;
                }

                var member = await db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == email);
                if (member == null || !member.IsEnabled || member.SecurityStamp != stamp)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync();
                    return;
                }

                // Role revocation check
                bool claimsAdmin = context.Principal?.IsInRole("Admin") ?? false;
                if (claimsAdmin != member.IsAdmin)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync();
                    return;
                }
            }
        };
    });

// Antiforgery
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

// Health Checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Ensure release of run lock on shutdown
if (runLockHandle != null)
{
    app.Lifetime.ApplicationStopping.Register(() =>
    {
        DemoLockManager.ReleaseRunLock();
    });
}

// Database Initialization & Seeding
if (isDemo)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        using (DemoLockManager.AcquireInitLock(demoDir, timeoutSeconds: 10))
        {
            var db = services.GetRequiredService<PetPetDbContext>();
            logger.LogInformation("正在執行 SQLite 資料庫 Migrations...");
            await db.Database.MigrateAsync();

            long seed = app.Configuration.GetValue<long>("Demo:Seed", 20261013);
            int version = app.Configuration.GetValue<int>("Demo:GeneratorVersion", 1);
            var baseTimeStr = app.Configuration["Demo:BaseTimeUtc"];
            DateTime baseTimeUtc = DateTime.TryParse(baseTimeStr, out var bt) ? bt : new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

            var seedResult = await DemoSeeder.SeedAsync(db, services, seed, baseTimeUtc, version);
            logger.LogInformation("Demo 初始化就緒。目前統計: {@Counts}", seedResult.CurrentCounts);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Demo 初始化失敗。");
        throw;
    }
}
else if (app.Environment.IsDevelopment())
{
    // In normal Development, ensure News exists if using localdb
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
    try
    {
        if (db.Database.CanConnect() && !await db.News.AnyAsync())
        {
            db.News.AddRange(
                new News { Title = "🎉 PetPet 全新改版上線！", Content = "歡迎來到 PetPet！", PublishedAt = DateTime.UtcNow },
                new News { Title = "⚠️ 系統公告", Content = "歡迎各位毛孩家長交流。", PublishedAt = DateTime.UtcNow.AddDays(-1) }
            );
            await db.SaveChangesAsync();
        }
    }
    catch
    {
        // Ignore if DB is not configured in dev
    }
}

// Pipeline
if (!app.Environment.IsDevelopment() && !isDemo)
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseRouting();

// Async form pre-reader for /Member/Login POST so rate limiter can read form synchronously without blocking
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/Member/Login") &&
        HttpMethods.IsPost(context.Request.Method) &&
        context.Request.HasFormContentType)
    {
        await context.Request.ReadFormAsync();
    }
    await next();
});

// Loopback guard in Demo mode
if (isDemo)
{
    app.Use(async (context, next) =>
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp != null && !IPAddress.IsLoopback(remoteIp))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Forbidden: Demo mode is restricted to loopback requests only.");
            return;
        }
        await next();
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseStaticFiles();

// Health check endpoints
app.MapGet("/health/live", () => Results.Text("live"));
app.MapGet("/health/ready", async (PetPetDbContext db) =>
{
    try
    {
        bool canConnect = await db.Database.CanConnectAsync();
        if (!canConnect) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

        if (isDemo)
        {
            bool hasSeedRecord = await db.SeedRecords.AnyAsync();
            if (!hasSeedRecord) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Text("ready");
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ChatHub>("/chatHub");

app.Run();

public partial class Program { }

internal sealed class ChainedRateLimiter : RateLimiter
{
    private readonly RateLimiter[] _limiters;
    public ChainedRateLimiter(params RateLimiter[] limiters) => _limiters = limiters;

    public override TimeSpan? IdleDuration => null;
    public override RateLimiterStatistics? GetStatistics() => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        var leases = new List<RateLimitLease>(_limiters.Length);
        for (int i = 0; i < _limiters.Length; i++)
        {
            var lease = _limiters[i].AttemptAcquire(permitCount);
            if (!lease.IsAcquired)
            {
                for (int j = 0; j < leases.Count; j++) leases[j].Dispose();
                return lease;
            }
            leases.Add(lease);
        }
        return new ChainedLease(leases);
    }

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(AttemptAcquireCore(permitCount));
    }

    private sealed class ChainedLease : RateLimitLease
    {
        private readonly List<RateLimitLease> _leases;
        public ChainedLease(List<RateLimitLease> leases) => _leases = leases;
        public override bool IsAcquired => true;
        public override IEnumerable<string> MetadataNames => Enumerable.Empty<string>();
        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            for (int i = 0; i < _leases.Count; i++)
            {
                if (_leases[i].TryGetMetadata(metadataName, out metadata))
                {
                    return true;
                }
            }
            metadata = null;
            return false;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                for (int i = 0; i < _leases.Count; i++) _leases[i].Dispose();
            }
        }
    }
}
