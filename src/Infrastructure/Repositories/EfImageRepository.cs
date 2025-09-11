using BuilderAssistantApi.Domain.Entities;
using BuilderAssistantApi.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BuilderAssistantApi.Infrastructure.Repositories;

/// <summary>
/// Entity Framework implementation of IImageRepository.
/// Provides data access operations for Image entities using BuilderAssistantDbContext.
/// </summary>
public class EfImageRepository : IImageRepository
{
    private readonly BuilderAssistantDbContext _context;

    public EfImageRepository(BuilderAssistantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Image?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.Images
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<IEnumerable<Image>> ListByProjectAsync(long projectId, CancellationToken ct = default)
    {
        return await _context.Images
            .AsNoTracking()
            .Where(i => i.ProjectId == projectId)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Image image, CancellationToken ct = default)
    {
        if (image == null)
            throw new ArgumentNullException(nameof(image));

        await _context.Images.AddAsync(image, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Image image, CancellationToken ct = default)
    {
        if (image == null)
            throw new ArgumentNullException(nameof(image));

        _context.Images.Update(image);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var image = await _context.Images.FindAsync(new object[] { id }, ct);
        if (image != null)
        {
            _context.Images.Remove(image);
            await _context.SaveChangesAsync(ct);
        }
    }
}