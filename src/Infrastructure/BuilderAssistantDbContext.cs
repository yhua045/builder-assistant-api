using BuilderAssistantApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BuilderAssistantApi.Infrastructure;

public class BuilderAssistantDbContext : DbContext
{
    public BuilderAssistantDbContext(DbContextOptions<BuilderAssistantDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Prompt> Prompts => Set<Prompt>();
    public DbSet<Image> Images => Set<Image>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(320); // RFC 5321 email max length

            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_Users_Email");

            entity.Property(e => e.DisplayName)
                .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            // One-to-many: User -> Projects
            entity.HasMany(e => e.Projects)
                .WithOne(e => e.Owner)
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Project configuration
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Description)
                .HasMaxLength(1000);

            entity.Property(e => e.OwnerId)
                .IsRequired();

            entity.HasIndex(e => e.OwnerId)
                .HasDatabaseName("IX_Projects_OwnerId");

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            // One-to-many: Project -> Prompts
            entity.HasMany(e => e.Prompts)
                .WithOne(e => e.Project)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // One-to-many: Project -> Images
            entity.HasMany(e => e.Images)
                .WithOne(e => e.Project)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Prompt configuration
        modelBuilder.Entity<Prompt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.ProjectId)
                .IsRequired();

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("IX_Prompts_ProjectId");

            entity.Property(e => e.Text)
                .IsRequired()
                .HasMaxLength(10000); // Large text field for prompts

            entity.Property(e => e.Model)
                .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                .IsRequired();
        });

        // Image configuration
        modelBuilder.Entity<Image>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.FileName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.ContentType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Size)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.Property(e => e.Category)
                .IsRequired()
                .HasDefaultValue(ImageCategory.Other);

            // ProjectId is nullable (images can exist without being assigned to a project)
            entity.Property(e => e.ProjectId)
                .IsRequired(false);

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("IX_Images_ProjectId");
        });
    }
}