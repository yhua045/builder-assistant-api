using Microsoft.Playwright;
using Xunit;

namespace BuilderAssistantApi.E2e.Tests;

[Collection(nameof(E2eCollection))]
public sealed class SmokeTests
{
    private readonly PlaywrightBrowserFixture _fixture;

    public SmokeTests(PlaywrightBrowserFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task LoginPage_RendersPublicLoginForm()
    {
        await using var context = await _fixture.CreateContextAsync();
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync("/Identity/Account/Login");

        Assert.NotNull(response);
        Assert.True(response!.Ok);
        Assert.Equal("Log in", await page.TitleAsync());
        Assert.Contains("Log in", await page.Locator("h2").InnerTextAsync());
        Assert.True(await page.Locator("input[name='Input.Email']").IsVisibleAsync());
        Assert.True(await page.Locator("button[type='submit']").IsVisibleAsync());
    }

    [Fact]
    public async Task UploadHealth_ReturnsHealthyResponse()
    {
        await using var context = await _fixture.CreateContextAsync();
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync("/uploads/health");

        Assert.NotNull(response);
        Assert.True(response!.Ok);
        Assert.Contains("Healthy", await page.Locator("body").InnerTextAsync());
    }
}
