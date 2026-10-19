using Microsoft.EntityFrameworkCore;
using PetPet.Application.Members;
using PetPet.Infrastructure.Data;
using PetPet.Infrastructure.Services;

namespace PetPet.UnitTests;

public class MemberAuthenticationTests
{
    [Theory]
    [InlineData("not-email", "password123")]
    [InlineData("valid@example.com", "short")]
    public async Task InvalidRegistration_DoesNotPersist(string email, string password)
    {
        using var db = CreateContext();
        var service = new MemberAccountService(new MemberRepository(db), new MemberPasswordHasher());
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(
            () => service.RegisterAsync(new(email, password, "Tester", "")));
        Assert.Empty(db.Members);
    }

    private static PetPetDbContext CreateContext() => new(new DbContextOptionsBuilder<PetPetDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Register_StoresHash_AndAuthenticatesOnlyCorrectPassword()
    {
        using var db = CreateContext();
        var service = new MemberAccountService(new MemberRepository(db), new MemberPasswordHasher());
        Assert.True(await service.RegisterAsync(new("tester@example.com", "password123", "Tester", "0912345678")));
        var stored = await db.Members.SingleAsync();
        Assert.NotEqual("password123", stored.Password);
        Assert.NotNull(await service.AuthenticateAsync(stored.Email, "password123"));
        Assert.Null(await service.AuthenticateAsync(stored.Email, "wrong"));
        Assert.Null(await service.AuthenticateAsync("missing@example.com", "password123"));
        stored.IsEnabled = false;
        await db.SaveChangesAsync();
        Assert.Null(await service.AuthenticateAsync(stored.Email, "password123"));
    }

    [Theory]
    [InlineData("password123")]
    [InlineData("not-a-valid-hash")]
    [InlineData("AAAA")]
    public async Task LegacyOrMalformedPassword_IsRejected(string storedPassword)
    {
        using var db = CreateContext();
        db.Members.Add(new() { Email = "legacy@example.com", Password = storedPassword, Name = "Legacy", Phone = "", IsEnabled = true });
        await db.SaveChangesAsync();
        var service = new MemberAccountService(new MemberRepository(db), new MemberPasswordHasher());
        Assert.Null(await service.AuthenticateAsync("legacy@example.com", storedPassword));
    }

    [Fact]
    public async Task DuplicateRegistration_DoesNotOverwriteAccount()
    {
        using var db = CreateContext();
        var service = new MemberAccountService(new MemberRepository(db), new MemberPasswordHasher());
        Assert.True(await service.RegisterAsync(new("tester@example.com", "password123", "Tester", "")));
        Assert.False(await service.RegisterAsync(new("tester@example.com", "different123", "Other", "")));
        Assert.Single(db.Members);
        Assert.NotNull(await service.AuthenticateAsync("tester@example.com", "password123"));
    }
}
