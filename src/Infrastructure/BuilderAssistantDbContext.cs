using BuilderAssistantApi.Domain.Constants;
using BuilderAssistantApi.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BuilderAssistantApi.Infrastructure;

public class BuilderAssistantDbContext : IdentityDbContext<User, IdentityRole<long>, long>
{
    public BuilderAssistantDbContext(DbContextOptions<BuilderAssistantDbContext> options) : base(options)
    {
    }

    // Projects, Prompts, Images... Users is already provided by IdentityDbContext
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Prompt> Prompts => Set<Prompt>();
    public DbSet<Image> Images => Set<Image>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<RoleEntitlement> RoleEntitlements => Set<RoleEntitlement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
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

        // Feature configuration
        modelBuilder.Entity<Feature>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.Key)
                .IsRequired()
                .HasMaxLength(100)
                .Metadata.SetAfterSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Throw);

            entity.HasIndex(e => e.Key)
                .IsUnique()
                .HasDatabaseName("UX_Features_Key");

            entity.Property(e => e.Description)
                .HasMaxLength(500);

            // Seed well-known features
            entity.HasData(
                new Feature { Id = 1, Key = FeatureKeys.OcrScan, Description = "OCR scan for invoices and receipts", DefaultEnabled = false },
                new Feature { Id = 2, Key = FeatureKeys.HighRateApi, Description = "High-rate API access", DefaultEnabled = false }
            );
        });

        // RoleEntitlement configuration
        modelBuilder.Entity<RoleEntitlement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.RoleName)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.FeatureKey)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Enabled)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.HasIndex(e => new { e.RoleName, e.FeatureKey })
                .IsUnique()
                .HasDatabaseName("IX_RoleEntitlements_RoleName_FeatureKey");
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