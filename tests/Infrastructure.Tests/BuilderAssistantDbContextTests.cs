using BuilderAssistantApi.Domain.Entities;
using BuilderAssistantApi.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests;

public class BuilderAssistantDbContextTests
{
    private BuilderAssistantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<BuilderAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new BuilderAssistantDbContext(options);
    }

    [Fact]
    public async Task CanCreateUserAndProject()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var user = new User
        {
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var project = new Project
        {
            Name = "Test Project",
            Description = "A test project",
            Owner = user,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        context.Users.Add(user);
        context.Projects.Add(project);
        await context.SaveChangesAsync();

        // Assert
        var savedUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
        var savedProject = await context.Projects.Include(p => p.Owner).FirstOrDefaultAsync(p => p.Name == "Test Project");

        Assert.NotNull(savedUser);
        Assert.NotNull(savedProject);
        Assert.Equal("test@example.com", savedUser.Email);
        Assert.Equal("Test Project", savedProject.Name);
        Assert.Equal(savedUser.Id, savedProject.OwnerId);
        Assert.Equal("test@example.com", savedProject.Owner?.Email);
    }

    [Fact]
    public async Task CanCreatePromptAndImage()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var user = new User
        {
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var project = new Project
        {
            Name = "Test Project",
            Owner = user,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var prompt = new Prompt
        {
            Text = "Generate a beautiful UI",
            Model = "gpt-4",
            Project = project,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var image = new Image
        {
            FileName = "test.png",
            ContentType = "image/png",
            Size = 1024,
            Category = ImageCategory.Architecture,
            Project = project,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        context.Users.Add(user);
        context.Projects.Add(project);
        context.Prompts.Add(prompt);
        context.Images.Add(image);
        await context.SaveChangesAsync();

        // Assert
        var savedPrompt = await context.Prompts.Include(p => p.Project).FirstOrDefaultAsync();
        var savedImage = await context.Images.Include(i => i.Project).FirstOrDefaultAsync();

        Assert.NotNull(savedPrompt);
        Assert.NotNull(savedImage);
        Assert.Equal("Generate a beautiful UI", savedPrompt.Text);
        Assert.Equal("gpt-4", savedPrompt.Model);
        Assert.Equal("test.png", savedImage.FileName);
        Assert.Equal(ImageCategory.Architecture, savedImage.Category);
        Assert.Equal(project.Id, savedPrompt.ProjectId);
        Assert.Equal(project.Id, savedImage.ProjectId);
    }

    [Fact]
    public async Task UserEmailMustBeUnique()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var user1 = new User
        {
            Email = "test@example.com",
            DisplayName = "Test User 1",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var user2 = new User
        {
            Email = "test@example.com", // Same email
            DisplayName = "Test User 2",
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act & Assert
        context.Users.Add(user1);
        await context.SaveChangesAsync();

        context.Users.Add(user2);

        // In-memory database doesn't enforce unique constraints the same way as SQL Server
        // But we can verify the configuration is correct by checking the model
        var entityType = context.Model.FindEntityType(typeof(User));
        var emailProperty = entityType?.FindProperty(nameof(User.Email));
        var emailIndex = entityType?.GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(User.Email)));

        Assert.NotNull(emailIndex);
        Assert.True(emailIndex.IsUnique);
    }
}