using BuilderAssistantApi.Domain.Entities;

namespace BuilderAssistantApi.Domain.Repositories;

/// <summary>
/// Repository interface for Image entity operations.
/// Provides abstraction over data persistence for Image entities.
/// </summary>
public interface IImageRepository
{
    /// <summary>
    /// Retrieves an image by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the image.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The image if found; otherwise, null.</returns>
    Task<Image?> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all images associated with a specific project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Collection of images belonging to the specified project.</returns>
    Task<IEnumerable<Image>> ListByProjectAsync(long projectId, CancellationToken ct = default);

    /// <summary>
    /// Adds a new image to the repository.
    /// </summary>
    /// <param name="image">The image entity to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(Image image, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing image in the repository.
    /// </summary>
    /// <param name="image">The image entity with updated values.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateAsync(Image image, CancellationToken ct = default);

    /// <summary>
    /// Deletes an image by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the image to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(long id, CancellationToken ct = default);
}