using BuilderAssistantApi.Domain.Entities;
using BuilderAssistantApi.Domain.Repositories;

namespace BuilderAssistantApi.Infrastructure.Repositories;

public sealed class EfRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly BuilderAssistantDbContext _dbContext;

    public EfRefreshTokenRepository(BuilderAssistantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        await _dbContext.RefreshTokens.AddAsync(token, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
