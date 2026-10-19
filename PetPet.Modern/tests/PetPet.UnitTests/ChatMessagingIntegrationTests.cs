using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PetPet.Application.Chat;
using PetPet.Application.Members;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;
using PetPet.Infrastructure.Services;

namespace PetPet.UnitTests;

[Trait("Requirement", "PP-MSG-01")]
[Trait("Requirement", "PP-MSG-02")]
[Trait("Requirement", "PP-MSG-03")]
public class ChatMessagingIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _sqliteConnection;
    private readonly WebApplicationFactory<Program> _factory;

    private Guid _alicePublicId;
    private Guid _bobPublicId;
    private Guid _charlieAdminPublicId;

    public ChatMessagingIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"petpet_chat_{Guid.NewGuid():N}.db");
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

        var charlie = new Member
        {
            Email = "charlie@petpet.test",
            NormalizedEmail = "CHARLIE@PETPET.TEST",
            Name = "CharlieAdmin",
            Password = string.Empty,
            IsEnabled = true,
            IsAdmin = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Version = Guid.NewGuid()
        };
        charlie.Password = hasher.Hash(charlie, "Password123!");

        db.Members.AddRange(alice, bob, charlie);
        db.SaveChanges();

        _alicePublicId = alice.PublicId;
        _bobPublicId = bob.PublicId;
        _charlieAdminPublicId = charlie.PublicId;
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

        var authCsrf = await ExtractAntiforgeryToken(client, "/Chat");
        return (client, authCsrf);
    }

    [Fact]
    public async Task Chat_OnlyAcceptedFriends_CanSendMessage_NonFriendsRejected()
    {
        var (aliceClient, aliceCsrf) = await CreateAuthenticatedClientAsync("alice@petpet.test", "Password123!");

        // 1. Non-friends: Alice tries to send message to Bob -> 400 Bad Request
        var sendReq = new SendMessageRequest
        {
            TargetPublicId = _bobPublicId,
            ClientMessageId = Guid.NewGuid(),
            Content = "哈囉 Bob，我們還不是好友！"
        };
        var reqMsg = new HttpRequestMessage(HttpMethod.Post, "/Chat/Send")
        {
            Content = JsonContent.Create(sendReq)
        };
        reqMsg.Headers.Add("X-CSRF-TOKEN", aliceCsrf);

        var res = await aliceClient.SendAsync(reqMsg);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        // 2. Establish Accepted friendship
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            db.Friends.Add(new Friend
            {
                RequesterEmail = "alice@petpet.test",
                AddresseeEmail = "bob@petpet.test",
                MemberAEmail = "alice@petpet.test",
                MemberBEmail = "bob@petpet.test",
                IsAccepted = true
            });
            await db.SaveChangesAsync();
        }

        // 3. Now accepted friends: Alice sends message to Bob -> 200 OK with ACK
        var clientMsgId = Guid.NewGuid();
        sendReq.ClientMessageId = clientMsgId;
        sendReq.Content = "哈囉 Bob，我們成為好友了！";

        reqMsg = new HttpRequestMessage(HttpMethod.Post, "/Chat/Send")
        {
            Content = JsonContent.Create(sendReq)
        };
        reqMsg.Headers.Add("X-CSRF-TOKEN", aliceCsrf);

        res = await aliceClient.SendAsync(reqMsg);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var ack = await res.Content.ReadFromJsonAsync<SendMessageResponse>();
        Assert.NotNull(ack);
        Assert.True(ack.Success);
        Assert.True(ack.MessageId > 0);
        Assert.Equal(clientMsgId, ack.ClientMessageId);
        Assert.False(ack.IsDuplicate);
    }

    [Fact]
    public async Task Chat_BlockedUser_CannotSendMessage_InEitherDirection()
    {
        // 1. Establish friend relationship
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            db.Friends.Add(new Friend
            {
                RequesterEmail = "alice@petpet.test",
                AddresseeEmail = "bob@petpet.test",
                MemberAEmail = "alice@petpet.test",
                MemberBEmail = "bob@petpet.test",
                IsAccepted = true
            });
            // Bob blocks Alice
            db.Blocks.Add(new Block
            {
                BlockerEmail = "bob@petpet.test",
                BlockedEmail = "alice@petpet.test"
            });
            await db.SaveChangesAsync();
        }

        var (aliceClient, aliceCsrf) = await CreateAuthenticatedClientAsync("alice@petpet.test", "Password123!");
        var (bobClient, bobCsrf) = await CreateAuthenticatedClientAsync("bob@petpet.test", "Password123!");

        // Alice tries to send to Bob -> Blocked (400)
        var aliceReq = new HttpRequestMessage(HttpMethod.Post, "/Chat/Send")
        {
            Content = JsonContent.Create(new SendMessageRequest
            {
                TargetPublicId = _bobPublicId,
                ClientMessageId = Guid.NewGuid(),
                Content = "被封鎖嘗試傳送"
            })
        };
        aliceReq.Headers.Add("X-CSRF-TOKEN", aliceCsrf);
        var aliceRes = await aliceClient.SendAsync(aliceReq);
        Assert.Equal(HttpStatusCode.BadRequest, aliceRes.StatusCode);

        // Bob tries to send to Alice -> Blocked (400)
        var bobReq = new HttpRequestMessage(HttpMethod.Post, "/Chat/Send")
        {
            Content = JsonContent.Create(new SendMessageRequest
            {
                TargetPublicId = _alicePublicId,
                ClientMessageId = Guid.NewGuid(),
                Content = "封鎖者嘗試傳送"
            })
        };
        bobReq.Headers.Add("X-CSRF-TOKEN", bobCsrf);
        var bobRes = await bobClient.SendAsync(bobReq);
        Assert.Equal(HttpStatusCode.BadRequest, bobRes.StatusCode);
    }

    [Fact]
    public async Task Chat_Deduplication_And_ConflictEnforcement()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            db.Friends.Add(new Friend
            {
                RequesterEmail = "alice@petpet.test",
                AddresseeEmail = "bob@petpet.test",
                MemberAEmail = "alice@petpet.test",
                MemberBEmail = "bob@petpet.test",
                IsAccepted = true
            });
            await db.SaveChangesAsync();
        }

        var (aliceClient, aliceCsrf) = await CreateAuthenticatedClientAsync("alice@petpet.test", "Password123!");
        var clientMsgId = Guid.NewGuid();

        // 1. Initial send
        var req1 = new HttpRequestMessage(HttpMethod.Post, "/Chat/Send")
        {
            Content = JsonContent.Create(new SendMessageRequest
            {
                TargetPublicId = _bobPublicId,
                ClientMessageId = clientMsgId,
                Content = "冪等重試測試訊息"
            })
        };
        req1.Headers.Add("X-CSRF-TOKEN", aliceCsrf);
        var res1 = await aliceClient.SendAsync(req1);
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
        var ack1 = await res1.Content.ReadFromJsonAsync<SendMessageResponse>();
        Assert.NotNull(ack1);
        Assert.False(ack1.IsDuplicate);

        // 2. Replay with identical content -> 200 OK with IsDuplicate = true
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/Chat/Send")
        {
            Content = JsonContent.Create(new SendMessageRequest
            {
                TargetPublicId = _bobPublicId,
                ClientMessageId = clientMsgId,
                Content = "冪等重試測試訊息"
            })
        };
        req2.Headers.Add("X-CSRF-TOKEN", aliceCsrf);
        var res2 = await aliceClient.SendAsync(req2);
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
        var ack2 = await res2.Content.ReadFromJsonAsync<SendMessageResponse>();
        Assert.NotNull(ack2);
        Assert.Equal(ack1.MessageId, ack2.MessageId);
        Assert.True(ack2.IsDuplicate);

        // Verify only 1 row in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            int count = await db.Messages.CountAsync(m => m.ClientMessageId == clientMsgId);
            Assert.Equal(1, count);
        }

        // 3. Conflict: same ClientMessageId but different content -> 409 Conflict
        var reqConflict = new HttpRequestMessage(HttpMethod.Post, "/Chat/Send")
        {
            Content = JsonContent.Create(new SendMessageRequest
            {
                TargetPublicId = _bobPublicId,
                ClientMessageId = clientMsgId,
                Content = "篡改之不同內容"
            })
        };
        reqConflict.Headers.Add("X-CSRF-TOKEN", aliceCsrf);
        var resConflict = await aliceClient.SendAsync(reqConflict);
        Assert.Equal(HttpStatusCode.Conflict, resConflict.StatusCode);
    }

    [Fact]
    public async Task Chat_History_CursorPagination_And_AdminCannotSpy()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            db.Friends.Add(new Friend
            {
                RequesterEmail = "alice@petpet.test",
                AddresseeEmail = "bob@petpet.test",
                MemberAEmail = "alice@petpet.test",
                MemberBEmail = "bob@petpet.test",
                IsAccepted = true
            });

            // Seed 60 messages between Alice and Bob
            var baseTime = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            for (int i = 1; i <= 60; i++)
            {
                db.Messages.Add(new Message
                {
                    SenderEmail = (i % 2 == 1) ? "alice@petpet.test" : "bob@petpet.test",
                    ReceiverEmail = (i % 2 == 1) ? "bob@petpet.test" : "alice@petpet.test",
                    ConversationPair = ChatService.GetConversationPair("alice@petpet.test", "bob@petpet.test"),
                    Content = $"訊息 #{i}",
                    SentAt = baseTime.AddMinutes(i),
                    ClientMessageId = Guid.NewGuid()
                });
            }
            await db.SaveChangesAsync();
        }

        var (aliceClient, _) = await CreateAuthenticatedClientAsync("alice@petpet.test", "Password123!");

        // 1. First 50 messages (latest 50, i.e. messages 11 to 60)
        var firstBatchRes = await aliceClient.GetAsync($"/Chat/History?targetPublicId={_bobPublicId}&limit=50");
        Assert.Equal(HttpStatusCode.OK, firstBatchRes.StatusCode);
        var firstBatch = await firstBatchRes.Content.ReadFromJsonAsync<List<ChatMessageDto>>();
        Assert.NotNull(firstBatch);
        Assert.Equal(50, firstBatch.Count);
        Assert.Equal("訊息 #11", firstBatch[0].Content);
        Assert.Equal("訊息 #60", firstBatch[49].Content);

        // 2. Load earlier messages with cursor (before first message of previous batch)
        var cursorTime = firstBatch[0].SentAt.ToString("o");
        int cursorId = firstBatch[0].Id;
        var secondBatchRes = await aliceClient.GetAsync($"/Chat/History?targetPublicId={_bobPublicId}&beforeSentAt={Uri.EscapeDataString(cursorTime)}&beforeId={cursorId}&limit=50");
        Assert.Equal(HttpStatusCode.OK, secondBatchRes.StatusCode);
        var secondBatch = await secondBatchRes.Content.ReadFromJsonAsync<List<ChatMessageDto>>();
        Assert.NotNull(secondBatch);
        Assert.Equal(10, secondBatch.Count); // remaining 10 messages (1 to 10)
        Assert.Equal("訊息 #1", secondBatch[0].Content);
        Assert.Equal("訊息 #10", secondBatch[9].Content);

        // 3. Admin privacy check: Charlie (admin) cannot spy on Alice and Bob's private messages
        var (charlieClient, _) = await CreateAuthenticatedClientAsync("charlie@petpet.test", "Password123!");
        var adminSpyRes = await charlieClient.GetAsync($"/Chat/History?targetPublicId={_bobPublicId}&limit=50");
        Assert.Equal(HttpStatusCode.OK, adminSpyRes.StatusCode);
        var adminSpyList = await adminSpyRes.Content.ReadFromJsonAsync<List<ChatMessageDto>>();
        Assert.NotNull(adminSpyList);
        Assert.Empty(adminSpyList); // Admin receives empty list, cannot read private chat
    }

    [Fact]
    public async Task Chat_MarkRead_UpdatesReceipts_Properly()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            db.Friends.Add(new Friend
            {
                RequesterEmail = "alice@petpet.test",
                AddresseeEmail = "bob@petpet.test",
                MemberAEmail = "alice@petpet.test",
                MemberBEmail = "bob@petpet.test",
                IsAccepted = true
            });

            // Alice sends 3 messages to Bob
            for (int i = 1; i <= 3; i++)
            {
                db.Messages.Add(new Message
                {
                    SenderEmail = "alice@petpet.test",
                    ReceiverEmail = "bob@petpet.test",
                    ConversationPair = ChatService.GetConversationPair("alice@petpet.test", "bob@petpet.test"),
                    Content = $"來自 Alice 的第 {i} 則訊息",
                    SentAt = DateTime.UtcNow.AddMinutes(i),
                    ClientMessageId = Guid.NewGuid()
                });
            }
            await db.SaveChangesAsync();
        }

        var (bobClient, bobCsrf) = await CreateAuthenticatedClientAsync("bob@petpet.test", "Password123!");

        // Bob marks messages up to Id = 2 as read
        int targetLastId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var msgs = await db.Messages.OrderBy(m => m.Id).ToListAsync();
            targetLastId = msgs[1].Id; // 2nd message
        }

        var markReq = new HttpRequestMessage(HttpMethod.Post, "/Chat/MarkRead")
        {
            Content = JsonContent.Create(new MarkReadRequest
            {
                TargetPublicId = _alicePublicId,
                UpToMessageId = targetLastId
            })
        };
        markReq.Headers.Add("X-CSRF-TOKEN", bobCsrf);
        var markRes = await bobClient.SendAsync(markReq);
        Assert.Equal(HttpStatusCode.OK, markRes.StatusCode);

        // Verify DB: messages 1 and 2 are read, message 3 is unread
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetPetDbContext>();
            var allMsgs = await db.Messages.OrderBy(m => m.Id).ToListAsync();
            Assert.NotNull(allMsgs[0].ReadAt);
            Assert.NotNull(allMsgs[1].ReadAt);
            Assert.Null(allMsgs[2].ReadAt);
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
