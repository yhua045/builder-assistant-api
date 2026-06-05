namespace BuilderAssistantApi.E2e.Tests;

public sealed record E2eRuntimeOptions(Uri BaseUrl, bool IgnoreHttpsErrors)
{
    public const string EnvironmentVariableName = "E2E_ENVIRONMENT";
    public const string BaseUrlVariableName = "E2E_BASE_URL";
    public const string LocalBaseUrlVariableName = "E2E_LOCAL_BASE_URL";
    public const string StagingBaseUrlVariableName = "E2E_STAGING_BASE_URL";
    public const string IgnoreHttpsErrorsVariableName = "E2E_IGNORE_HTTPS_ERRORS";

    public static E2eRuntimeOptions FromEnvironment()
    {
        var environmentName = (Environment.GetEnvironmentVariable(EnvironmentVariableName) ?? "local").Trim().ToLowerInvariant();
        var baseUrl = ResolveBaseUrl(environmentName);
        var ignoreHttpsErrors = ParseBoolean(Environment.GetEnvironmentVariable(IgnoreHttpsErrorsVariableName));

        return new E2eRuntimeOptions(baseUrl, ignoreHttpsErrors);
    }

    private static Uri ResolveBaseUrl(string environmentName)
    {
        var overrideBaseUrl = Environment.GetEnvironmentVariable(BaseUrlVariableName);
        if (!string.IsNullOrWhiteSpace(overrideBaseUrl))
        {
            return NormalizeBaseUrl(overrideBaseUrl);
        }

        return environmentName switch
        {
            "local" => NormalizeBaseUrl(Environment.GetEnvironmentVariable(LocalBaseUrlVariableName) ?? "http://localhost:5001"),
            "staging" => NormalizeBaseUrl(
                Environment.GetEnvironmentVariable(StagingBaseUrlVariableName)
                ?? throw new InvalidOperationException(
                    $"Set {BaseUrlVariableName} or {StagingBaseUrlVariableName} before running staging E2E tests.")),
            _ => throw new InvalidOperationException(
                $"Unsupported {EnvironmentVariableName} value '{environmentName}'. Use 'local' or 'staging'.")
        };
    }

    private static Uri NormalizeBaseUrl(string value)
    {
        var trimmed = value.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid base URL '{value}'. Provide an absolute HTTP or HTTPS URL.");
        }

        return uri;
    }

    private static bool ParseBoolean(string? value)
    {
        return bool.TryParse(value, out var parsed) && parsed;
    }
}
