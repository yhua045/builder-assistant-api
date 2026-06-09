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
    public async Task LoginFlow_UsesOtpTokenFromDevOnlyEndpoint()
    {
        if (!_fixture.RuntimeOptions.CanUseOtpTestEndpoint)
        {
            return;
        }

        const string email = "owner@builderassistant.dev";

        await using var context = await _fixture.CreateContextAsync();
        var page = await context.NewPageAsync();
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        var apiRequest = await playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            BaseURL = _fixture.RuntimeOptions.BaseUrl.ToString(),
            IgnoreHTTPSErrors = _fixture.RuntimeOptions.IgnoreHttpsErrors
        });

        try
        {
            await page.GotoAsync("/Identity/Account/Login");
            await page.Locator("input[name='Input.Email']").FillAsync(email);
            await page.Locator("button[type='submit']").ClickAsync();

            await page.WaitForURLAsync("**/Identity/Account/VerifyOtp**");

            var otpResponse = await apiRequest.GetAsync($"/test/identity/otp?email={Uri.EscapeDataString(email)}");

            Assert.NotNull(otpResponse);
            Assert.True(otpResponse!.Ok);

            var otpPayload = await otpResponse.JsonAsync();
            Assert.NotNull(otpPayload);

            var otpToken = otpPayload!.Value.GetProperty("otpToken").GetString();

            Assert.False(string.IsNullOrWhiteSpace(otpToken));

            await page.Locator("input[name='Input.Otp']").FillAsync(otpToken!);
            await page.Locator("button[type='submit']").ClickAsync();

            await page.WaitForURLAsync("**/");

            Assert.Equal($"{_fixture.RuntimeOptions.BaseUrl}", page.Url);
        }
        finally
        {
            await apiRequest.DisposeAsync();
        }
    }

    [Fact]
    public async Task LoginPage_RendersPublicLoginForm()
    {
        await using var context = await _fixture.CreateContextAsync();
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync("/Identity/Account/Login");

        Assert.NotNull(response);
        Assert.True(response!.Ok);
        Assert.Equal("Log in - Builder Assistant", await page.TitleAsync());
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
