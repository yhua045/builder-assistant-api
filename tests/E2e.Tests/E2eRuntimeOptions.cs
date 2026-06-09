namespace BuilderAssistantApi.E2e.Tests;

public sealed record E2eRuntimeOptions(string EnvironmentName, Uri BaseUrl, bool IgnoreHttpsErrors)
{
    public const string EnvironmentVariableName = "E2E_ENVIRONMENT";
    public const string BaseUrlVariableName = "E2E_BASE_URL";
    public const string LocalBaseUrlVariableName = "E2E_LOCAL_BASE_URL";
    public const string StagingBaseUrlVariableName = "E2E_STAGING_BASE_URL";
    public const string IgnoreHttpsErrorsVariableName = "E2E_IGNORE_HTTPS_ERRORS";

    public bool CanUseOtpTestEndpoint => EnvironmentName is "development" or "test";

    public static E2eRuntimeOptions FromEnvironment()
    {
        var environmentName = (Environment.GetEnvironmentVariable(EnvironmentVariableName) ?? "development").Trim().ToLowerInvariant();
        var baseUrl = ResolveBaseUrl(environmentName);
        var ignoreHttpsErrors = ParseBoolean(Environment.GetEnvironmentVariable(IgnoreHttpsErrorsVariableName));

        return new E2eRuntimeOptions(environmentName, baseUrl, ignoreHttpsErrors);
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
            "development" => NormalizeBaseUrl(Environment.GetEnvironmentVariable(LocalBaseUrlVariableName) ?? "http://localhost:5001"),
            "test" => NormalizeBaseUrl(
                Environment.GetEnvironmentVariable(StagingBaseUrlVariableName)
                ?? throw new InvalidOperationException(
                    $"Set {BaseUrlVariableName} or {StagingBaseUrlVariableName} before running test E2E tests.")),
            _ => throw new InvalidOperationException(
                $"Unsupported {EnvironmentVariableName} value '{environmentName}'. Use 'development' or 'test'.")
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
