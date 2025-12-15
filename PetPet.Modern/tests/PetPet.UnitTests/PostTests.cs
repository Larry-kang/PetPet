using Microsoft.EntityFrameworkCore;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;
using Xunit;

namespace PetPet.UnitTests;

public class PostTests
{
    private DbContextOptions<PetPetDbContext> _options;

    public PostTests()
    {
        _options = new DbContextOptionsBuilder<PetPetDbContext>()
            .UseInMemoryDatabase(databaseName: "PostTestDB_" + Guid.NewGuid()) // Unique DB
            .Options;
    }

    [Fact]
    public async Task AddPost_WithLike_Should_CountCorrectly()
    {
        // Arrange
        using var context = new PetPetDbContext(_options);
        var member = new Member { Email = "author@pet.com", Name = "Author", Password="pw", Phone="123" };
        var post = new Post 
        { 
            Title = "My Pet", 
            Content = "Cute!", 
            AuthorEmail = "author@pet.com", 
            CreatedAt = DateTime.UtcNow,
            Author = member
        };
        
        context.Members.Add(member);
        context.Posts.Add(post);
        await context.SaveChangesAsync();

        // Act - User A likes the post
        var like = new Like { PostId = post.Id, UserEmail = "author@pet.com" };
        context.Likes.Add(like);
        await context.SaveChangesAsync();

        // Assert
        using var verifyContext = new PetPetDbContext(_options);
        var savedPost = await verifyContext.Posts
            .Include(p => p.Likes)
            .FirstOrDefaultAsync(p => p.Id == post.Id);

        Assert.NotNull(savedPost);
        Assert.Single(savedPost!.Likes);
        Assert.Equal("author@pet.com", savedPost.Likes.First().UserEmail);
    }
}
