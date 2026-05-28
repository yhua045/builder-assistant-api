using BuilderAssistantApi.Domain.Entities;
using BuilderAssistantApi.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BuilderAssistantApi.Infrastructure.Repositories;

public sealed class EfAuthorizationCodeRepository : IAuthorizationCodeRepository
{
    private readonly BuilderAssistantDbContext _dbContext;

    public EfAuthorizationCodeRepository(BuilderAssistantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<AuthorizationCode?> FindValidByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return _dbContext.AuthorizationCodes
            .FirstOrDefaultAsync(c => c.Code == code && !c.IsUsed && c.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken);
    }

    public async Task AddAsync(AuthorizationCode code, CancellationToken cancellationToken = default)
    {
        await _dbContext.AuthorizationCodes.AddAsync(code, cancellationToken);
    }

    public Task MarkUsedAsync(AuthorizationCode code, CancellationToken cancellationToken = default)
    {
        code.IsUsed = true;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
