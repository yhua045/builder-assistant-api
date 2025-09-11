using BuilderAssistantApi.Domain.Entities;
using BuilderAssistantApi.Infrastructure;
using BuilderAssistantApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests;

public class EfImageRepositoryTests
{
    private BuilderAssistantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<BuilderAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new BuilderAssistantDbContext(options);
    }

    [Fact]
    public async Task AddAsync_ShouldAddImageToDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new EfImageRepository(context);

        var image = new Image
        {
            FileName = "test-image.png",
            ContentType = "image/png",
            Size = 2048,
            Category = ImageCategory.Architecture,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        await repository.AddAsync(image);

        // Assert
        var savedImage = await context.Images.FirstOrDefaultAsync(i => i.FileName == "test-image.png");
        Assert.NotNull(savedImage);
        Assert.Equal("test-image.png", savedImage.FileName);
        Assert.Equal("image/png", savedImage.ContentType);
        Assert.Equal(2048, savedImage.Size);
        Assert.Equal(ImageCategory.Architecture, savedImage.Category);
        Assert.True(savedImage.Id > 0);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnImageWhenExists()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new EfImageRepository(context);

        var image = new Image
        {
            FileName = "existing-image.jpg",
            ContentType = "image/jpeg",
            Size = 1024,
            Category = ImageCategory.Structured,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Images.Add(image);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetByIdAsync(image.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(image.Id, result.Id);
        Assert.Equal("existing-image.jpg", result.FileName);
        Assert.Equal("image/jpeg", result.ContentType);
        Assert.Equal(1024, result.Size);
        Assert.Equal(ImageCategory.Structured, result.Category);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNullWhenNotExists()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new EfImageRepository(context);

        // Act
        var result = await repository.GetByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ListByProjectAsync_ShouldReturnImagesForProject()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new EfImageRepository(context);

        // Create a user and project first
        var user = new User
        {
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var project = new Project
        {
            Name = "Test Project",
            Description = "Test project description",
            Owner = user,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Users.Add(user);
        context.Projects.Add(project);
        await context.SaveChangesAsync();

        // Create images - some for the project, some without project
        var image1 = new Image
        {
            FileName = "project-image-1.png",
            ContentType = "image/png",
            Size = 1024,
            Category = ImageCategory.Architecture,
            ProjectId = project.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var image2 = new Image
        {
            FileName = "project-image-2.jpg",
            ContentType = "image/jpeg",
            Size = 2048,
            Category = ImageCategory.Structured,
            ProjectId = project.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var image3 = new Image
        {
            FileName = "standalone-image.png",
            ContentType = "image/png",
            Size = 512,
            Category = ImageCategory.Other,
            ProjectId = null, // Not associated with any project
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Images.AddRange(image1, image2, image3);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.ListByProjectAsync(project.Id);

        // Assert
        var images = result.ToList();
        Assert.Equal(2, images.Count);
        Assert.Contains(images, i => i.FileName == "project-image-1.png");
        Assert.Contains(images, i => i.FileName == "project-image-2.jpg");
        Assert.DoesNotContain(images, i => i.FileName == "standalone-image.png");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateImageInDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new EfImageRepository(context);

        var image = new Image
        {
            FileName = "original-name.png",
            ContentType = "image/png",
            Size = 1024,
            Category = ImageCategory.Other,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Images.Add(image);
        await context.SaveChangesAsync();

        // Modify the image
        image.FileName = "updated-name.png";
        image.Category = ImageCategory.Architecture;
        image.Size = 2048;

        // Act
        await repository.UpdateAsync(image);

        // Assert
        var updatedImage = await context.Images.FindAsync(image.Id);
        Assert.NotNull(updatedImage);
        Assert.Equal("updated-name.png", updatedImage.FileName);
        Assert.Equal(ImageCategory.Architecture, updatedImage.Category);
        Assert.Equal(2048, updatedImage.Size);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveImageFromDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new EfImageRepository(context);

        var image = new Image
        {
            FileName = "to-be-deleted.png",
            ContentType = "image/png",
            Size = 1024,
            Category = ImageCategory.Other,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Images.Add(image);
        await context.SaveChangesAsync();

        // Act
        await repository.DeleteAsync(image.Id);

        // Assert
        var deletedImage = await context.Images.FindAsync(image.Id);
        Assert.Null(deletedImage);
    }

    [Fact]
    public async Task DeleteAsync_ShouldNotThrowWhenImageNotExists()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new EfImageRepository(context);

        // Act & Assert - should not throw exception
        await repository.DeleteAsync(999);
    }
}