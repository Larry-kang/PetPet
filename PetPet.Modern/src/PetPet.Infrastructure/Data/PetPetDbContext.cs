using Microsoft.EntityFrameworkCore;
using PetPet.Domain.Entities;

namespace PetPet.Infrastructure.Data;

public class PetPetDbContext : DbContext
{
    public PetPetDbContext(DbContextOptions<PetPetDbContext> options) : base(options)
    {
    }

    public DbSet<Member> Members => Set<Member>();
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<News> News => Set<News>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Like> Likes => Set<Like>();
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Member Configuration
        modelBuilder.Entity<Member>(entity =>
        {
            entity.ToTable("Member");
            entity.HasKey(e => e.Email);
            entity.Property(e => e.Email).HasMaxLength(254).IsRequired();
            entity.Property(e => e.Password).HasMaxLength(4000).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Photo).HasMaxLength(1000);
            
            // Relationships
            entity.HasMany(d => d.Pets)
                  .WithOne(p => p.Owner)
                  .HasForeignKey(p => p.OwnerEmail)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(d => d.Posts)
                  .WithOne(p => p.Author)
                  .HasForeignKey(p => p.AuthorEmail)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Pet Configuration
        modelBuilder.Entity<Pet>(entity =>
        {
            entity.ToTable("Pet");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Pet_no");
            entity.Property(e => e.OwnerEmail).HasColumnName("Email").IsRequired();
            entity.Property(e => e.VarietyId).HasColumnName("PetVariety_no");
            entity.Property(e => e.Name).HasColumnName("Pet_name").HasMaxLength(40).IsRequired();
            entity.Property(e => e.Gender).HasColumnName("Pet_gender");
            entity.Property(e => e.Photo).HasColumnName("Pet_photo").HasMaxLength(1000);
        });

        // Comment Relationship
        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasOne(c => c.Author)
                  .WithMany()
                  .HasForeignKey(c => c.UserEmail)
                  .OnDelete(DeleteBehavior.Restrict); // Corrected: Restrict to prevent cycle

            entity.HasOne(c => c.Post)
                  .WithMany(p => p.Comments)
                  .HasForeignKey(c => c.PostId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Like Configuration
        modelBuilder.Entity<Like>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(l => l.User)
                  .WithMany() 
                  .HasForeignKey(l => l.UserEmail)
                  .OnDelete(DeleteBehavior.Restrict); // Prevent cycle

             entity.HasOne(l => l.Post)
                   .WithMany(p => p.Likes)
                   .HasForeignKey(l => l.PostId)
                   .OnDelete(DeleteBehavior.Cascade);
        });

        // Post Configuration
        modelBuilder.Entity<Post>(entity =>
        {
            entity.ToTable("Post");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Post_no");
            entity.Property(e => e.AuthorEmail).HasColumnName("Post_Email").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("Post_time");
            entity.Property(e => e.Title).HasColumnName("Post_title").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Content).HasColumnName("Post_content").HasMaxLength(4000).IsRequired();
            entity.Property(e => e.IsEnabled).HasColumnName("Post_Enable");
        });
    }
}
