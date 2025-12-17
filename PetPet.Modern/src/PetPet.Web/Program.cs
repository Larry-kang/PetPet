using Microsoft.EntityFrameworkCore;
using MassTransit;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options => 
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddScoped<PetPet.Infrastructure.Services.IMatchService, PetPet.Infrastructure.Services.MatchService>();
builder.Services.AddSingleton<PetPet.Application.Services.ZiweiService>(); // Register ZiweiService

// MassTransit & RabbitMQ Configuration
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PetPet.Infrastructure.Consumers.NotificationConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddDbContext<PetPet.Infrastructure.Data.PetPetDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSignalR(); // Add SignalR

builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Member/Login";
        options.LogoutPath = "/Member/Logout";
    });



var app = builder.Build();

// Auto-Create DB for Zero Friction
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<PetPet.Infrastructure.Data.PetPetDbContext>();
        context.Database.EnsureDeleted(); // Re-create DB to apply new Schema without Migrations
        context.Database.EnsureCreated();

        // Seed Master Data (PetType & Variety)
        if (!context.PetTypes.Any())
        {
            var dogType = new PetPet.Domain.Entities.PetType { Name = "狗" };
            var catType = new PetPet.Domain.Entities.PetType { Name = "貓" };
            context.PetTypes.AddRange(dogType, catType);
            context.SaveChanges();

            context.PetVarieties.AddRange(
                new PetPet.Domain.Entities.PetVariety { Name = "黃金獵犬", PetTypeId = dogType.Id },
                new PetPet.Domain.Entities.PetVariety { Name = "拉布拉多", PetTypeId = dogType.Id },
                new PetPet.Domain.Entities.PetVariety { Name = "柴犬", PetTypeId = dogType.Id },
                new PetPet.Domain.Entities.PetVariety { Name = "貴賓狗", PetTypeId = dogType.Id },
                new PetPet.Domain.Entities.PetVariety { Name = "米克斯 (狗)", PetTypeId = dogType.Id },
                new PetPet.Domain.Entities.PetVariety { Name = "波斯貓", PetTypeId = catType.Id },
                new PetPet.Domain.Entities.PetVariety { Name = "暹羅貓", PetTypeId = catType.Id },
                new PetPet.Domain.Entities.PetVariety { Name = "美國短毛貓", PetTypeId = catType.Id },
                new PetPet.Domain.Entities.PetVariety { Name = "米克斯 (貓)", PetTypeId = catType.Id }
            );
            context.SaveChanges();
        }

        // Seed News
        if (!context.News.Any())
        {
            context.News.AddRange(
                new PetPet.Domain.Entities.News 
                { 
                    Title = "🎉 PetPet 全新改版上線！", 
                    Content = "親愛的會員您好，我們很高興地宣布 PetPet 全新改版正式上線！\n更漂亮的介面、更流暢的體驗，快來試試看吧！", 
                    PublishedAt = DateTime.UtcNow 
                },
                new PetPet.Domain.Entities.News 
                { 
                    Title = "⚠️ 系統維護公告", 
                    Content = "將於 12/31 凌晨 02:00 進行例行維護，預計暫停服務 2 小時。", 
                    PublishedAt = DateTime.UtcNow.AddDays(-1) 
                }
            );
            // Seed Members & Posts for Demo
            if (!context.Members.Any())
            {
                var alice = new PetPet.Domain.Entities.Member
                {
                    Email = "alice@test.com",
                    Name = "Alice",
                    Password = "password", // In production use hash
                    Phone = "0900000001",
                    Gender = false, // Female
                    Birthday = DateTime.UtcNow.AddYears(-25),
                    CityId = 1,
                    IsEnabled = true
                };
                var bob = new PetPet.Domain.Entities.Member
                {
                    Email = "bob@test.com",
                    Name = "Bob",
                    Password = "password",
                    Phone = "0900000002",
                    Gender = true, // Male
                    Birthday = DateTime.UtcNow.AddYears(-30),
                    CityId = 2,
                    IsEnabled = true
                };
                var admin = new PetPet.Domain.Entities.Member
                {
                    Email = "admin@petpet.com",
                    Name = "Administrator",
                    Password = "admin",
                    Phone = "0900000000",
                    Gender = true,
                    Birthday = DateTime.UtcNow,
                    CityId = 1,
                    IsEnabled = true,
                    IsAdmin = true
                };
                
                var spy = new PetPet.Domain.Entities.Member
                {
                    Email = "spy@test.com",
                    Name = "Spy User",
                    Password = "password",
                    Phone = "0900000099",
                    Gender = false,
                    Birthday = DateTime.UtcNow,
                    CityId = 1,
                    IsEnabled = true,
                    IsAdmin = false
                };

                context.Members.AddRange(alice, bob, admin, spy);
                context.SaveChanges();



                context.SaveChanges();

                // Advanced Seeding (Taiwanese Simulated Users)
                var extraMembers = new List<PetPet.Domain.Entities.Member>
                {
                    new() { Email="chen@test.com", Name="陳志明", Password="password", Phone="0911000001", Gender=true, Birthday=DateTime.Now.AddYears(-28), CityId=1, IsEnabled=true, Photo = "/images/presets/avatar-boy.png" }, // Taipei
                    new() { Email="lin@test.com", Name="林雅婷", Password="password", Phone="0911000002", Gender=false, Birthday=DateTime.Now.AddYears(-24), CityId=2, IsEnabled=true, Photo = "/images/presets/avatar-girl.png" }, // Taichung
                    new() { Email="lee@test.com", Name="李建國", Password="password", Phone="0911000003", Gender=true, Birthday=DateTime.Now.AddYears(-35), CityId=3, IsEnabled=true, Photo = "/images/presets/avatar-boy.png" }, // Kaohsiung
                    new() { Email="chang@test.com", Name="張淑芬", Password="password", Phone="0911000004", Gender=false, Birthday=DateTime.Now.AddYears(-29), CityId=1, IsEnabled=true, Photo = "/images/presets/avatar-girl.png" }, // Taipei
                    new() { Email="wang@test.com", Name="王冠宇", Password="password", Phone="0911000005", Gender=true, Birthday=DateTime.Now.AddYears(-30), CityId=2, IsEnabled=true, Photo = "/images/presets/avatar-boy.png" }, // Taichung
                    new() { Email="wu@test.com", Name="吳心怡", Password="password", Phone="0911000006", Gender=false, Birthday=DateTime.Now.AddYears(-22), CityId=3, IsEnabled=true, Photo = "/images/presets/avatar-girl.png" }, // Kaohsiung
                    new() { Email="liu@test.com", Name="劉宗翰", Password="password", Phone="0911000007", Gender=true, Birthday=DateTime.Now.AddYears(-26), CityId=4, IsEnabled=true, Photo = "/images/presets/avatar-boy.png" }, // Tainan?
                    new() { Email="tsai@test.com", Name="蔡婉婷", Password="password", Phone="0911000008", Gender=false, Birthday=DateTime.Now.AddYears(-25), CityId=5, IsEnabled=true, Photo = "/images/presets/avatar-girl.png" }, // Hsinchu?
                    new() { Email="yang@test.com", Name="楊家豪", Password="password", Phone="0911000009", Gender=true, Birthday=DateTime.Now.AddYears(-27), CityId=1, IsEnabled=true, Photo = "/images/presets/avatar-boy.png" }, // Taipei
                    new() { Email="huang@test.com", Name="黃思穎", Password="password", Phone="0911000010", Gender=false, Birthday=DateTime.Now.AddYears(-23), CityId=2, IsEnabled=true, Photo = "/images/presets/avatar-girl.png" }, // Taichung
                    new() { Email="ai@petpet.com", Name="PetPet AI 助手", Password="password", Phone="0900000000", Gender=true, Birthday=DateTime.Now, CityId=1, IsEnabled=true, Photo = "/images/presets/avatar-ai-robot.png" }, // AI Bot
                };

                // Mass Seeding (500 Random Users)
                var random = new Random();
                var lastNames = new[] { "陳", "林", "黃", "張", "李", "王", "吳", "劉", "蔡", "楊", "許", "鄭", "謝", "郭", "洪", "曾", "邱", "廖", "賴", "徐" };
                var firstNamesM = new[] { "志明", "建國", "冠宇", "宗翰", "家豪", "俊傑", "彥廷", "承恩", "柏宇", "品睿", "宇軒", "冠廷", "子軒", "家偉", "柏翰", "信宏", "志偉", "建宏", "智偉" };
                var firstNamesF = new[] { "雅婷", "淑芬", "心怡", "婉婷", "思穎", "詩涵", "雅雯", "心愛", "宜蓁", "佳穎", "怡君", "欣怡", "雅琪", "佩君", "欣儀", "鈺婷", "郁婷", "詩雅", "家he" };
                
                var totalUsers = 500;
                var generatedEmails = new List<string>();

                for (int i = 11; i <= totalUsers; i++)
                {
                    bool isMale = random.Next(2) == 0;
                    string lastName = lastNames[random.Next(lastNames.Length)];
                    string firstName = isMale ? firstNamesM[random.Next(firstNamesM.Length)] : firstNamesF[random.Next(firstNamesF.Length)];
                    string name = lastName + firstName;
                    string photo = isMale ? $"/images/presets/avatar-boy.png" : $"/images/presets/avatar-girl.png";
                    var email = $"user{i}@test.com";
                    
                    extraMembers.Add(new PetPet.Domain.Entities.Member
                    {
                        Email = email,
                        Name = name,
                        Password = "password",
                        Phone = $"09{random.Next(10000000, 99999999)}",
                        Gender = isMale,
                        Birthday = DateTime.UtcNow.AddYears(-random.Next(20, 50)),
                        CityId = random.Next(1, 6),
                        IsEnabled = true,
                        Photo = photo
                    });
                    generatedEmails.Add(email);
                }
                
                context.Members.AddRange(extraMembers);
                context.SaveChanges();

                // Seed Pets
                var pets = new List<PetPet.Domain.Entities.Pet>
                {
                    new() { OwnerEmail = "alice@test.com", Name="Buddy", Gender=true, VarietyId=1, Photo="/images/presets/avatar-dog.png" }, // Dog
                    new() { OwnerEmail = "bob@test.com", Name="Mittens", Gender=false, VarietyId=2, Photo="/images/presets/avatar-cat.png" }, // Cat
                    
                    new() { OwnerEmail = "chen@test.com", Name="小黑 (Kuro)", Gender=true, VarietyId=1, Photo="/images/presets/avatar-dog.png" },
                    new() { OwnerEmail = "lin@test.com", Name="咪咪 (Mimi)", Gender=false, VarietyId=2, Photo="/images/presets/avatar-cat.png" },
                    new() { OwnerEmail = "lee@test.com", Name="來福 (Lucky)", Gender=true, VarietyId=1, Photo="/images/presets/avatar-dog.png" },
                    new() { OwnerEmail = "chang@test.com", Name="豆豆", Gender=false, VarietyId=1, Photo="/images/presets/avatar-dog.png" }, // Dog
                    new() { OwnerEmail = "wang@test.com", Name="阿肥", Gender=true, VarietyId=2, Photo="/images/presets/avatar-cat.png" }, // Cat
                    new() { OwnerEmail = "wu@test.com", Name="麻糬", Gender=false, VarietyId=2, Photo="/images/presets/avatar-cat.png" },
                    new() { OwnerEmail = "liu@test.com", Name="皮皮", Gender=true, VarietyId=1, Photo="/images/presets/avatar-dog.svg" },
                    new() { OwnerEmail = "tsai@test.com", Name="球球", Gender=false, VarietyId=1, Photo="/images/presets/avatar-dog.svg" },
                    new() { OwnerEmail = "yang@test.com", Name="虎斑", Gender=true, VarietyId=2, Photo="/images/presets/avatar-cat.svg" },
                    new() { OwnerEmail = "huang@test.com", Name="布丁", Gender=false, VarietyId=2, Photo="/images/presets/avatar-cat.svg" },
                };

                // Mass Seeding Pets (1 per user)
                var petVarieties = await context.PetVarieties.ToListAsync();
                foreach (var member in extraMembers)
                {    
                     if (member.Email.StartsWith("user"))
                     {
                         var variety = petVarieties[random.Next(petVarieties.Count)];
                         var isDog = variety.PetTypeId == 1; // Assuming 1 is Dog
                         var petName = isDog ? "忠犬" : "愛貓";
                         var petPhoto = isDog ? "/images/presets/avatar-dog.svg" : "/images/presets/avatar-cat.svg";
                         
                         pets.Add(new PetPet.Domain.Entities.Pet 
                         { 
                             OwnerEmail = member.Email, 
                             Name = $"{petName}-{random.Next(1,99)}", 
                             Gender = random.Next(2) == 0, 
                             VarietyId = variety.Id, 
                             Photo = petPhoto 
                         });
                     }
                }

                context.Pets.AddRange(pets);
                context.SaveChanges();

                // Seed Posts (1000 Posts)
                var postContents = new[] 
                { 
                    "今天天氣真好，帶毛孩出去跑跑！ ☀️", 
                    "這家寵物餐廳真的大推，食物好吃環境又好！ 🍖", 
                    "請問大家的狗狗都吃什麼牌子的飼料呢？求推薦 🙏", 
                    "剛洗完澡的樣子，是不是超級可愛？ 😍", 
                    "今天發生了一件趣事...", 
                    "養寵物真的需要很大的耐心，但一切都是值得的 ❤️", 
                    "這是我家毛孩的睡姿大賞 😂", 
                    "週末就是要睡到自然醒～ 💤", 
                    "有人也住在大安森林公園附近嗎？可以一起溜狗喔！", 
                    "新的玩具不到五分鐘就報銷了... 💸" 
                };

                var posts = new List<PetPet.Domain.Entities.Post>();
                // Include manual created members in the pool
                var allMemberEmails = extraMembers.Select(m => m.Email).Concat(new[] { "alice@test.com", "bob@test.com", "ai@petpet.com" }).ToList();

                for (int i = 0; i < 1000; i++)
                {
                    var authorEmail = allMemberEmails[random.Next(allMemberEmails.Count)];
                    var content = postContents[random.Next(postContents.Length)];
                    var hasImage = random.Next(2) == 0;
                    var created = DateTime.UtcNow.AddDays(-random.Next(0, 30)).AddHours(random.Next(0, 24));
                    
                    var post = new PetPet.Domain.Entities.Post
                    {
                        AuthorEmail = authorEmail,
                        Title = $"生活隨筆 #{i}",
                        Content = content,
                        CreatedAt = created,
                        IsEnabled = true,
                        ImageUrl = hasImage ? (random.Next(2) == 0 ? "/images/presets/avatar-dog.svg" : "/images/presets/avatar-cat.svg") : null
                    };
                    posts.Add(post);
                }

                context.Posts.AddRange(posts);
                context.SaveChanges();

                // Seed Likes & Comments
                var comments = new List<PetPet.Domain.Entities.Comment>();
                var likes = new List<PetPet.Domain.Entities.Like>();
                
                foreach (var post in posts)
                {
                    // Random Likes (0-20)
                    int likeCount = random.Next(0, 21);
                    for (int k = 0; k < likeCount; k++)
                    {
                         string likerEmail = allMemberEmails[random.Next(allMemberEmails.Count)];
                         // Ensure unique like per user per post (simplified check)
                         if (!likes.Any(l => l.PostId == post.Id && l.UserEmail == likerEmail))
                         {
                             likes.Add(new PetPet.Domain.Entities.Like { PostId = post.Id, UserEmail = likerEmail });
                         }
                    }

                    // Random Comments (0-5)
                    int commentCount = random.Next(0, 6);
                    for (int k = 0; k < commentCount; k++)
                    {
                        string commenterEmail = allMemberEmails[random.Next(allMemberEmails.Count)];
                        comments.Add(new PetPet.Domain.Entities.Comment 
                        { 
                            PostId = post.Id, 
                            UserEmail = commenterEmail, 
                            Content = "真的太可愛了！ ❤️", 
                            CreatedAt = post.CreatedAt.AddMinutes(random.Next(1, 1000)) 
                        });
                    }
                }
                
                context.Likes.AddRange(likes);
                context.Comments.AddRange(comments);
                context.SaveChanges();

                // Seed Matches (Ensure EVERYONE has at least 3 matches + AI Match)
                var matches = new List<PetPet.Domain.Entities.MatchInteraction>();
                var messages = new List<PetPet.Domain.Entities.Message>();
                var aiEmail = "ai@petpet.com";

                // Ensure AI User Exists in List (it was added above)
                
                foreach (var currentUser in allMemberEmails)
                {
                    if (currentUser == aiEmail) continue;

                    // 1. Force Match with AI
                    matches.Add(new PetPet.Domain.Entities.MatchInteraction { SourceMemberId = currentUser, TargetMemberId = aiEmail, Action = PetPet.Domain.Entities.MatchAction.Like, CreatedAt = DateTime.UtcNow });
                    matches.Add(new PetPet.Domain.Entities.MatchInteraction { SourceMemberId = aiEmail, TargetMemberId = currentUser, Action = PetPet.Domain.Entities.MatchAction.Like, CreatedAt = DateTime.UtcNow });
                    
                    // 2. Initial Message from AI
                    messages.Add(new PetPet.Domain.Entities.Message 
                    { 
                        SenderEmail = aiEmail, 
                        ReceiverEmail = currentUser, 
                        Content = "嗨！我是 PetPet 專屬的 AI 助手 🤖\n歡迎來到這個溫暖的寵物社群！\n有任何問題或是想聊聊毛小孩，隨時都可以找我喔！", 
                        SentAt = DateTime.UtcNow, 
                        IsRead = false 
                    });

                    // Pick 3 random targets for each user to have mutual match with (besides AI)
                    for (int k = 0; k < 3; k++)
                    {
                        string targetUser;
                        do
                        {
                            targetUser = allMemberEmails[random.Next(allMemberEmails.Count)];
                        } while (targetUser == currentUser || targetUser == aiEmail); // Ensure not self and not AI (AI is already handled)

                        // Check if already matched to avoid duplicates in list (simple check)
                        bool alreadyMatched = matches.Any(m => 
                            (m.SourceMemberId == currentUser && m.TargetMemberId == targetUser) || 
                            (m.SourceMemberId == targetUser && m.TargetMemberId == currentUser));

                        if (!alreadyMatched)
                        {
                            // A likes B
                            matches.Add(new PetPet.Domain.Entities.MatchInteraction { SourceMemberId = currentUser, TargetMemberId = targetUser, Action = PetPet.Domain.Entities.MatchAction.Like, CreatedAt = DateTime.UtcNow });
                            // B likes A (Mutual)
                            matches.Add(new PetPet.Domain.Entities.MatchInteraction { SourceMemberId = targetUser, TargetMemberId = currentUser, Action = PetPet.Domain.Entities.MatchAction.Like, CreatedAt = DateTime.UtcNow });
                        }
                    }
                }
                context.MatchInteractions.AddRange(matches);
                context.Messages.AddRange(messages);
                context.SaveChanges();

                // Seed Friendships (Alice <-> Bob, Alice <-> Chen)
                context.Friends.AddRange(
                    new PetPet.Domain.Entities.Friend { RequesterEmail = "alice@test.com", AddresseeEmail = "bob@test.com", IsAccepted = true },
                    new PetPet.Domain.Entities.Friend { RequesterEmail = "chen@test.com", AddresseeEmail = "alice@test.com", IsAccepted = true }
                );

                context.SaveChanges();
            }
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating the DB.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// app.UseHttpsRedirection(); // Disabled for Docker HTTP specific setup
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<PetPet.Web.Hubs.ChatHub>("/chatHub"); // Map Hub


app.Run();
