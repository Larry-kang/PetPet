using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<PetPet.Infrastructure.Data.PetPetDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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
            context.SaveChanges();
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

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
