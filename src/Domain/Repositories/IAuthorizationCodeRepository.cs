using BuilderAssistantApi.Domain.Entities;

namespace BuilderAssistantApi.Domain.Repositories;

public interface IAuthorizationCodeRepository
{
    Task<AuthorizationCode?> FindValidByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task AddAsync(AuthorizationCode code, CancellationToken cancellationToken = default);
    Task MarkUsedAsync(AuthorizationCode code, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
