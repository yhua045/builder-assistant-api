using Xunit;

namespace BuilderAssistantApi.E2e.Tests;

[CollectionDefinition(nameof(E2eCollection), DisableParallelization = true)]
public sealed class E2eCollection : ICollectionFixture<PlaywrightBrowserFixture>
{
}
