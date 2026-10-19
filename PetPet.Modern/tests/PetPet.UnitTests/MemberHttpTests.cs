using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PetPet.Infrastructure.Data;

namespace PetPet.UnitTests;

public class MemberHttpTests
{
    private sealed class TestApp : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<PetPetDbContext>>();
                var name = Guid.NewGuid().ToString();
                services.AddDbContext<PetPetDbContext>(options => options.UseInMemoryDatabase(name));
            });
        }
    }

    private static async Task<string> Token(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        return WebUtility.HtmlDecode(Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    }

    [Fact]
    public async Task RegistrationAndLogin_IssueCookie_AndDoNotGrantAdmin()
    {
        using var app = new TestApp();
        using var client = app.CreateClient(new() { AllowAutoRedirect = false });
        var token = await Token(client, "/Member/Register");
        Assert.NotEmpty(token);
        var registration = await client.PostAsync("/Member/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "http@example.com", ["Password"] = "password123",
            ["Name"] = "HTTP Tester", ["Phone"] = "0912345678", ["IsAdmin"] = "true",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, registration.StatusCode);
        Assert.Equal("/Member/Login", registration.Headers.Location?.OriginalString);
        var loginToken = await Token(client, "/Member/Login");
        var login = await client.PostAsync("/Member/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "http@example.com", ["password"] = "password123", ["__RequestVerificationToken"] = loginToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), cookie => cookie.Contains(".AspNetCore.Cookies="));
        var admin = await client.GetAsync("/Admin/Index");
        Assert.Equal(HttpStatusCode.Redirect, admin.StatusCode);
        Assert.Contains("AccessDenied", admin.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Login_RequiresAntiforgery_AndRejectsWrongPassword()
    {
        using var app = new TestApp();
        using var client = app.CreateClient(new() { AllowAutoRedirect = false });
        var missingToken = await client.PostAsync("/Member/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "alice@test.com", ["password"] = "password"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
        var token = await Token(client, "/Member/Login");
        var wrongPassword = await client.PostAsync("/Member/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "alice@test.com", ["password"] = "wrong", ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.OK, wrongPassword.StatusCode);
        Assert.False(wrongPassword.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.Contains(".AspNetCore.Cookies=")));
    }
}
