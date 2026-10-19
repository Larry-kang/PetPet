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
    public DbSet<Friend> Friends => Set<Friend>();
    public DbSet<Block> Blocks => Set<Block>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<AdminAudit> AdminAudits => Set<AdminAudit>();
    public DbSet<SeedRecord> SeedRecords => Set<SeedRecord>();
    public DbSet<OperationReceipt> OperationReceipts => Set<OperationReceipt>();
    public DbSet<Media> Media => Set<Media>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Variety> Varieties => Set<Variety>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Member
        modelBuilder.Entity<Member>(entity =>
        {
            entity.ToTable("Member");
            entity.HasKey(e => e.Email);
            entity.Property(e => e.Email).HasMaxLength(254).IsRequired();
            entity.Property(e => e.NormalizedEmail).HasMaxLength(254).IsRequired();
            entity.HasIndex(e => e.NormalizedEmail).IsUnique();
            entity.Property(e => e.PublicId).IsRequired();
            entity.HasIndex(e => e.PublicId).IsUnique();

            entity.Property(e => e.Password).HasMaxLength(4000).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Bio).HasMaxLength(500);
            entity.Property(e => e.Photo).HasMaxLength(1000);
            entity.Property(e => e.SecurityStamp).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Version).IsRequired().IsConcurrencyToken();

            entity.HasMany(d => d.Pets)
                  .WithOne(p => p.Owner)
                  .HasForeignKey(p => p.OwnerEmail)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(d => d.Posts)
                  .WithOne(p => p.Author)
                  .HasForeignKey(p => p.AuthorEmail)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Pet
        modelBuilder.Entity<Pet>(entity =>
        {
            entity.ToTable("Pet");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Pet_no");
            entity.Property(e => e.OwnerEmail).HasColumnName("Email").HasMaxLength(254).IsRequired();
            entity.Property(e => e.VarietyId).HasColumnName("PetVariety_no");
            entity.Property(e => e.KindCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Name).HasColumnName("Pet_name").HasMaxLength(40).IsRequired();
            entity.Property(e => e.Gender).HasColumnName("Pet_gender");
            entity.Property(e => e.Bio).HasMaxLength(500);
            entity.Property(e => e.Photo).HasColumnName("Pet_photo").HasMaxLength(1000);
            entity.Property(e => e.Version).IsRequired().IsConcurrencyToken();

            entity.HasIndex(e => new { e.OwnerEmail, e.Id });
        });

        // Post
        modelBuilder.Entity<Post>(entity =>
        {
            entity.ToTable("Post");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Post_no");
            entity.Property(e => e.AuthorEmail).HasColumnName("Post_Email").HasMaxLength(254).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("Post_time");
            entity.Property(e => e.Title).HasColumnName("Post_title").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Content).HasColumnName("Post_content").HasMaxLength(4000).IsRequired();
            entity.Property(e => e.ImageUrl).HasColumnName("Post_img").HasMaxLength(1000);
            entity.Property(e => e.IsEnabled).HasColumnName("Post_Enable");
            entity.Property(e => e.Version).IsRequired().IsConcurrencyToken();

            entity.HasIndex(e => new { e.IsEnabled, e.IsDeleted, e.CreatedAt, e.Id });
        });

        // Comment
        modelBuilder.Entity<Comment>(entity =>
        {
            entity.ToTable("Comment");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserEmail).HasMaxLength(254).IsRequired();
            entity.Property(e => e.Content).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Version).IsRequired().IsConcurrencyToken();

            entity.HasOne(c => c.Author)
                  .WithMany()
                  .HasForeignKey(c => c.UserEmail)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.Post)
                  .WithMany(p => p.Comments)
                  .HasForeignKey(c => c.PostId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.PostId, e.IsDeleted, e.CreatedAt, e.Id });
        });

        // Like
        modelBuilder.Entity<Like>(entity =>
        {
            entity.ToTable("Like");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserEmail).HasMaxLength(254).IsRequired();

            entity.HasOne(l => l.User)
                  .WithMany()
                  .HasForeignKey(l => l.UserEmail)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.Post)
                  .WithMany(p => p.Likes)
                  .HasForeignKey(l => l.PostId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.PostId, e.UserEmail }).IsUnique();
        });

        // Friend
        modelBuilder.Entity<Friend>(entity =>
        {
            entity.ToTable("Friend");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RequesterEmail).HasMaxLength(254).IsRequired();
            entity.Property(e => e.AddresseeEmail).HasMaxLength(254).IsRequired();
            entity.Property(e => e.MemberAEmail).HasMaxLength(254).IsRequired();
            entity.Property(e => e.MemberBEmail).HasMaxLength(254).IsRequired();
            entity.Property(e => e.Version).IsRequired().IsConcurrencyToken();

            entity.HasOne(f => f.Requester)
                  .WithMany()
                  .HasForeignKey(f => f.RequesterEmail)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(f => f.Addressee)
                  .WithMany()
                  .HasForeignKey(f => f.AddresseeEmail)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(f => new { f.MemberAEmail, f.MemberBEmail }).IsUnique();
            entity.HasIndex(f => new { f.RequesterEmail, f.IsAccepted });
            entity.HasIndex(f => new { f.AddresseeEmail, f.IsAccepted });
        });

        // Block
        modelBuilder.Entity<Block>(entity =>
        {
            entity.ToTable("Block");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BlockerEmail).HasMaxLength(254).IsRequired();
            entity.Property(e => e.BlockedEmail).HasMaxLength(254).IsRequired();

            entity.HasOne(b => b.Blocker)
                  .WithMany()
                  .HasForeignKey(b => b.BlockerEmail)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Blocked)
                  .WithMany()
                  .HasForeignKey(b => b.BlockedEmail)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(b => new { b.BlockerEmail, b.BlockedEmail }).IsUnique();
        });

        // Notification
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notification");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RecipientEmail).HasMaxLength(254).IsRequired();
            entity.Property(e => e.ActorEmail).HasMaxLength(254);
            entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
            entity.Property(e => e.EventKey).HasMaxLength(200).IsRequired();
            entity.Property(e => e.TargetType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Summary).HasMaxLength(500).IsRequired();

            entity.HasOne(n => n.Recipient)
                  .WithMany()
                  .HasForeignKey(n => n.RecipientEmail)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(n => n.Actor)
                  .WithMany()
                  .HasForeignKey(n => n.ActorEmail)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(n => new { n.RecipientEmail, n.EventKey }).IsUnique();
            entity.HasIndex(n => new { n.RecipientEmail, n.ReadAt, n.CreatedAt, n.Id });
        });

        // Message
        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("Message");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SenderEmail).HasMaxLength(254).IsRequired();
            entity.Property(e => e.ReceiverEmail).HasMaxLength(254).IsRequired();
            entity.Property(e => e.ConversationPair).HasMaxLength(510).IsRequired();
            entity.Property(e => e.Content).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.ClientMessageId).IsRequired();
            entity.Ignore(e => e.IsRead);

            entity.HasOne(m => m.Sender)
                  .WithMany()
                  .HasForeignKey(m => m.SenderEmail)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Receiver)
                  .WithMany()
                  .HasForeignKey(m => m.ReceiverEmail)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(m => new { m.SenderEmail, m.ClientMessageId }).IsUnique();
            entity.HasIndex(m => new { m.ConversationPair, m.SentAt, m.Id });
        });

        // Report
        modelBuilder.Entity<Report>(entity =>
        {
            entity.ToTable("Report");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReporterEmail).HasMaxLength(254).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.HandledByEmail).HasMaxLength(254);
            entity.Property(e => e.HandlingNote).HasMaxLength(1000);
            entity.Property(e => e.Version).IsRequired().IsConcurrencyToken();

            entity.HasOne(r => r.Post)
                  .WithMany()
                  .HasForeignKey(r => r.PostId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(r => r.Reporter)
                  .WithMany()
                  .HasForeignKey(r => r.ReporterEmail)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.HandledBy)
                  .WithMany()
                  .HasForeignKey(r => r.HandledByEmail)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(r => new { r.Status, r.CreatedAt, r.Id });
            entity.HasIndex(r => new { r.ReporterEmail, r.PostId });
        });

        // AdminAudit
        modelBuilder.Entity<AdminAudit>(entity =>
        {
            entity.ToTable("AdminAudit");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ActorEmail).HasMaxLength(254).IsRequired();
            entity.Property(e => e.TargetId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Action).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.TraceId).HasMaxLength(100).IsRequired();

            entity.HasOne(a => a.Actor)
                  .WithMany()
                  .HasForeignKey(a => a.ActorEmail)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // SeedRecord
        modelBuilder.Entity<SeedRecord>(entity =>
        {
            entity.ToTable("SeedRecord");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BusinessFingerprint).HasMaxLength(64).IsRequired();
        });

        // OperationReceipt
        modelBuilder.Entity<OperationReceipt>(entity =>
        {
            entity.ToTable("OperationReceipt");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ActorEmail).HasMaxLength(254).IsRequired();
            entity.Property(e => e.Operation).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PayloadDigest).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ResultCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.TargetId).HasMaxLength(100);

            entity.HasOne(o => o.Actor)
                  .WithMany()
                  .HasForeignKey(o => o.ActorEmail)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(o => new { o.ActorEmail, o.Operation, o.CommandId }).IsUnique();
        });

        // Media
        modelBuilder.Entity<Media>(entity =>
        {
            entity.ToTable("Media");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OwnerEmail).HasMaxLength(254).IsRequired();
            entity.Property(e => e.StorageKey).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(100).IsRequired();

            entity.HasOne(m => m.Owner)
                  .WithMany()
                  .HasForeignKey(m => m.OwnerEmail)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // News
        modelBuilder.Entity<News>(entity =>
        {
            entity.ToTable("News");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(100).IsRequired();
        });

        // City
        modelBuilder.Entity<City>(entity =>
        {
            entity.ToTable("City");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Code).HasMaxLength(10).IsRequired();
        });

        // Variety
        modelBuilder.Entity<Variety>(entity =>
        {
            entity.ToTable("Variety");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.KindCode).HasMaxLength(20).IsRequired();
        });
    }
}
