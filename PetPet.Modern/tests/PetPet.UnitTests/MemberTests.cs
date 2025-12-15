using Microsoft.EntityFrameworkCore;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;
using Xunit;

namespace PetPet.UnitTests;

public class MemberTests
{
    private DbContextOptions<PetPetDbContext> _options;

    public MemberTests()
    {
        _options = new DbContextOptionsBuilder<PetPetDbContext>()
            .UseInMemoryDatabase(databaseName: "MemberTestDB")
            .Options;
    }

    [Fact]
    public async Task AddMember_Should_SaveToDatabase()
    {
        // Arrange
        using var context = new PetPetDbContext(_options);
        var member = new Member
        {
            Email = "test@example.com",
            Password = "password",
            Name = "Tester",
            Birthday = DateTime.Now,
            Gender = true,
            Phone = "0912345678",
            CityId = 1,
            Photo = "default.jpg",
            IsEnabled = true,
            IsMatchEnabled = true
        };

        // Act
        context.Members.Add(member);
        await context.SaveChangesAsync();

        // Assert
        using var verifyContext = new PetPetDbContext(_options);
        var savedMember = await verifyContext.Members.FirstOrDefaultAsync(m => m.Email == "test@example.com");
        Assert.NotNull(savedMember);
        Assert.Equal("Tester", savedMember?.Name);
    }
}
