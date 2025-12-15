using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<PetPet.Infrastructure.Services.IMatchService, PetPet.Infrastructure.Services.MatchService>();

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
        // context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

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

                context.Members.AddRange(alice, bob, admin);
                context.SaveChanges();



                // Advanced Seeding (Taiwanese Simulated Users)
                var extraMembers = new List<PetPet.Domain.Entities.Member>
                {
                    new() { Email="chen@test.com", Name="陳志明", Password="password", Phone="0911000001", Gender=true, Birthday=DateTime.Now.AddYears(-28), CityId=1, IsEnabled=true, Photo = "/images/presets/avatar-boy.svg" }, // Taipei
                    new() { Email="lin@test.com", Name="林雅婷", Password="password", Phone="0911000002", Gender=false, Birthday=DateTime.Now.AddYears(-24), CityId=2, IsEnabled=true, Photo = "/images/presets/avatar-girl.svg" }, // Taichung
                    new() { Email="lee@test.com", Name="李建國", Password="password", Phone="0911000003", Gender=true, Birthday=DateTime.Now.AddYears(-35), CityId=3, IsEnabled=true, Photo = "/images/presets/avatar-boy.svg" }, // Kaohsiung
                    new() { Email="chang@test.com", Name="張淑芬", Password="password", Phone="0911000004", Gender=false, Birthday=DateTime.Now.AddYears(-29), CityId=1, IsEnabled=true, Photo = "/images/presets/avatar-girl.svg" }, // Taipei
                    new() { Email="wang@test.com", Name="王冠宇", Password="password", Phone="0911000005", Gender=true, Birthday=DateTime.Now.AddYears(-30), CityId=2, IsEnabled=true, Photo = "/images/presets/avatar-boy.svg" }, // Taichung
                    new() { Email="wu@test.com", Name="吳心怡", Password="password", Phone="0911000006", Gender=false, Birthday=DateTime.Now.AddYears(-22), CityId=3, IsEnabled=true, Photo = "/images/presets/avatar-girl.svg" }, // Kaohsiung
                    new() { Email="liu@test.com", Name="劉宗翰", Password="password", Phone="0911000007", Gender=true, Birthday=DateTime.Now.AddYears(-26), CityId=4, IsEnabled=true, Photo = "/images/presets/avatar-boy.svg" }, // Tainan?
                    new() { Email="tsai@test.com", Name="蔡婉婷", Password="password", Phone="0911000008", Gender=false, Birthday=DateTime.Now.AddYears(-25), CityId=5, IsEnabled=true, Photo = "/images/presets/avatar-girl.svg" }, // Hsinchu?
                    new() { Email="yang@test.com", Name="楊家豪", Password="password", Phone="0911000009", Gender=true, Birthday=DateTime.Now.AddYears(-27), CityId=1, IsEnabled=true, Photo = "/images/presets/avatar-boy.svg" }, // Taipei
                    new() { Email="huang@test.com", Name="黃思穎", Password="password", Phone="0911000010", Gender=false, Birthday=DateTime.Now.AddYears(-23), CityId=2, IsEnabled=true, Photo = "/images/presets/avatar-girl.svg" }, // Taichung
                };
                context.Members.AddRange(extraMembers);
                context.SaveChanges();

                // Seed Pets
                var pets = new List<PetPet.Domain.Entities.Pet>
                {
                    new() { OwnerEmail = "alice@test.com", Name="Buddy", Gender=true, VarietyId=1, Photo="/images/presets/avatar-dog.svg" }, // Dog
                    new() { OwnerEmail = "bob@test.com", Name="Mittens", Gender=false, VarietyId=2, Photo="/images/presets/avatar-cat.svg" }, // Cat
                    
                    new() { OwnerEmail = "chen@test.com", Name="小黑 (Kuro)", Gender=true, VarietyId=1, Photo="/images/presets/avatar-dog.svg" },
                    new() { OwnerEmail = "lin@test.com", Name="咪咪 (Mimi)", Gender=false, VarietyId=2, Photo="/images/presets/avatar-cat.svg" },
                    new() { OwnerEmail = "lee@test.com", Name="來福 (Lucky)", Gender=true, VarietyId=1, Photo="/images/presets/avatar-dog.svg" },
                    new() { OwnerEmail = "chang@test.com", Name="豆豆", Gender=false, VarietyId=1, Photo="/images/presets/avatar-dog.svg" }, // Dog
                    new() { OwnerEmail = "wang@test.com", Name="阿肥", Gender=true, VarietyId=2, Photo="/images/presets/avatar-cat.svg" }, // Cat
                    new() { OwnerEmail = "wu@test.com", Name="麻糬", Gender=false, VarietyId=2, Photo="/images/presets/avatar-cat.svg" },
                    new() { OwnerEmail = "liu@test.com", Name="皮皮", Gender=true, VarietyId=1, Photo="/images/presets/avatar-dog.svg" },
                    new() { OwnerEmail = "tsai@test.com", Name="球球", Gender=false, VarietyId=1, Photo="/images/presets/avatar-dog.svg" },
                    new() { OwnerEmail = "yang@test.com", Name="虎斑", Gender=true, VarietyId=2, Photo="/images/presets/avatar-cat.svg" },
                    new() { OwnerEmail = "huang@test.com", Name="布丁", Gender=false, VarietyId=2, Photo="/images/presets/avatar-cat.svg" },
                };
                context.Pets.AddRange(pets);

                // Seed Posts
                context.Posts.AddRange(
                    new PetPet.Domain.Entities.Post { Title = "第一次養狗就上手", Content = "今天帶 Buddy 去打預防針，牠好勇敢都沒有哭！ 💉🐶", AuthorEmail = alice.Email, CreatedAt = DateTime.UtcNow.AddHours(-10), IsEnabled = true, ImageUrl = "/images/presets/avatar-dog.svg" },
                    new PetPet.Domain.Entities.Post { Title = "貓咪真的很傲嬌", Content = "Mittens 今天又不理我了，只有吃飯的時候才會過來蹭。 😅🐱", AuthorEmail = bob.Email, CreatedAt = DateTime.UtcNow.AddHours(-8), IsEnabled = true, ImageUrl = "/images/presets/avatar-cat.svg" },
                    new PetPet.Domain.Entities.Post { Title = "小黑的生日派對", Content = "小黑滿三歲了！買了一個大蛋糕給牠吃。 🎂", AuthorEmail = "chen@test.com", CreatedAt = DateTime.UtcNow.AddHours(-6), IsEnabled = true, ImageUrl = "/images/presets/avatar-dog.svg" },
                    new PetPet.Domain.Entities.Post { Title = "推薦超好用的貓砂", Content = "最近換了這個牌子的貓砂，除臭效果真的不錯，推薦給大家！ 👍", AuthorEmail = "lin@test.com", CreatedAt = DateTime.UtcNow.AddHours(-5), IsEnabled = true, ImageUrl = "/images/presets/avatar-cat.svg" },
                    new PetPet.Domain.Entities.Post { Title = "高雄哪裡適合溜狗？", Content = "剛搬來高雄，想請問大家有沒有推薦的寵物公園？ 🌳", AuthorEmail = "lee@test.com", CreatedAt = DateTime.UtcNow.AddHours(-4), IsEnabled = true },
                    new PetPet.Domain.Entities.Post { Title = "豆豆睡覺的樣子好可愛", Content = "看牠睡得這麼熟，都不忍心叫醒牠了。 💤", AuthorEmail = "chang@test.com", CreatedAt = DateTime.UtcNow.AddHours(-3), IsEnabled = true, ImageUrl = "/images/presets/avatar-dog.svg" },
                    new PetPet.Domain.Entities.Post { Title = "阿肥又變胖了...", Content = "是不是該幫牠減肥了？大家有什麼好方法嗎？ 🍖", AuthorEmail = "wang@test.com", CreatedAt = DateTime.UtcNow.AddHours(-2), IsEnabled = true, ImageUrl = "/images/presets/avatar-cat.svg" },
                    new PetPet.Domain.Entities.Post { Title = "尋找貓友", Content = "有沒有人也住附近，可以一起交流養貓心得？ 🤝", AuthorEmail = "wu@test.com", CreatedAt = DateTime.UtcNow.AddHours(-1), IsEnabled = true },
                    new PetPet.Domain.Entities.Post { Title = "皮皮學會新技能了！", Content = "牠現在會握手了喔！真是太聰明了！ 👏", AuthorEmail = "liu@test.com", CreatedAt = DateTime.UtcNow.AddMinutes(-30), IsEnabled = true, ImageUrl = "/images/presets/avatar-dog.svg" },
                    new PetPet.Domain.Entities.Post { Title = "週末寵物聚會", Content = "這個週末在中央公園有寵物聚會，大家要一起來嗎？ 🎉", AuthorEmail = "tsai@test.com", CreatedAt = DateTime.UtcNow.AddMinutes(-15), IsEnabled = true }
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
