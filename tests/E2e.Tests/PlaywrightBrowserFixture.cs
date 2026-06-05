using Microsoft.Playwright;
using Xunit;

namespace BuilderAssistantApi.E2e.Tests;

public sealed class PlaywrightBrowserFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public PlaywrightBrowserFixture()
    {
        RuntimeOptions = E2eRuntimeOptions.FromEnvironment();
    }

    public E2eRuntimeOptions RuntimeOptions { get; }

    public async Task InitializeAsync()
    {
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task<IBrowserContext> CreateContextAsync()
    {
        if (_browser is null)
        {
            throw new InvalidOperationException("Playwright browser is not initialized.");
        }

        return await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = RuntimeOptions.BaseUrl.ToString(),
            IgnoreHTTPSErrors = RuntimeOptions.IgnoreHttpsErrors
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }
}
