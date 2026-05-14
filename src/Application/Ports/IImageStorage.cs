using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BuilderAssistantApi.Application.Ports;

/// <summary>
/// Abstraction for image storage operations.
/// Implementations may store images via HTTP, cloud blob, or local filesystem.
/// </summary>
public interface IImageStorage
{
    /// <summary>
    /// Uploads an image and returns a public (or signed) URI for access.
    /// </summary>
    Task<Uri> UploadAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Gets a (signed) URL for an existing image by id, or null if not available.
    /// </summary>
    Task<Uri?> GetUrlAsync(long imageId, CancellationToken ct = default);

    /// <summary>
    /// Deletes an image by id.
    /// </summary>
    Task DeleteAsync(long imageId, CancellationToken ct = default);
}
