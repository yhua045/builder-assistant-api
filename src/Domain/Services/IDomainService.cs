using Microsoft.Extensions.Logging;

namespace BuilderAssistantApi.Domain.Services;

public interface IDomainService
{
    void ValidateBusinessRule(string rule);
}

public class DomainService : IDomainService
{
    private readonly ILogger<DomainService> _logger;

    public DomainService(ILogger<DomainService> logger)
    {
        _logger = logger;
    }

    public void ValidateBusinessRule(string rule)
    {
        _logger.LogDebug("Validating business rule: {Rule}", rule);

        // Example domain validation logic
        if (string.IsNullOrWhiteSpace(rule))
        {
            _logger.LogWarning("Business rule validation failed: Rule cannot be empty");
            throw new ArgumentException("Business rule cannot be empty", nameof(rule));
        }

        _logger.LogInformation("Business rule validation successful: {Rule}", rule);
    }
}