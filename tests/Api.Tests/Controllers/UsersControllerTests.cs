using BuilderAssistantApi.Api.Controllers;
using BuilderAssistantApi.Application.Services;
using BuilderAssistantApi.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace BuilderAssistantApi.Api.Tests.Controllers;

public sealed class UsersControllerTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Mock<UserManager<User>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<User>>();
#pragma warning disable CS8625 // null for optional ctor params that Moq handles
        return new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625
    }

    private static UsersController CreateController(
        Mock<IUserRegistrationService> service,
        Mock<UserManager<User>>? userManager = null)
    {
        var um = userManager ?? CreateUserManagerMock();
        return new UsersController(service.Object, um.Object);
    }

    // ── Register ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidRequest_Returns201()
    {
        var serviceMock = new Mock<IUserRegistrationService>();
        serviceMock
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistrationResult(true, new RegisterResponse(1, "user@example.com"), []));

        var controller = CreateController(serviceMock);

        var result = await controller.Register(new RegisterRequest("user@example.com", "Password123!"), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, created.StatusCode);
        var body = Assert.IsType<RegisterResponse>(created.Value);
        Assert.Equal(1, body.Id);
        Assert.Equal("user@example.com", body.Email);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var serviceMock = new Mock<IUserRegistrationService>();
        serviceMock
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistrationResult(false, null, ["Email is already registered."]));

        var controller = CreateController(serviceMock);

        var result = await controller.Register(new RegisterRequest("dupe@example.com", "Password123!"), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
    }

    [Fact]
    public async Task Register_WeakPassword_Returns422()
    {
        var serviceMock = new Mock<IUserRegistrationService>();
        serviceMock
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistrationResult(false, null, ["Password is too short."]));

        var controller = CreateController(serviceMock);

        var result = await controller.Register(new RegisterRequest("user@example.com", "x"), CancellationToken.None);

        var unprocessable = Assert.IsType<UnprocessableEntityObjectResult>(result);
        Assert.Equal(422, unprocessable.StatusCode);
    }

    // ── ConfirmEmail ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmEmail_ValidToken_Returns200()
    {
        var serviceMock = new Mock<IUserRegistrationService>();
        serviceMock
            .Setup(s => s.ConfirmEmailAsync(1, "good-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfirmEmailResult(true, []));

        var controller = CreateController(serviceMock);

        var result = await controller.ConfirmEmail(new ConfirmEmailRequest(1, "good-token"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_InvalidToken_Returns400()
    {
        var serviceMock = new Mock<IUserRegistrationService>();
        serviceMock
            .Setup(s => s.ConfirmEmailAsync(1, "bad-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfirmEmailResult(false, ["Invalid token."]));

        var controller = CreateController(serviceMock);

        var result = await controller.ConfirmEmail(new ConfirmEmailRequest(1, "bad-token"), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
    }

    // ── VerifyTwoFactor ──────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyTwoFactor_ValidOtp_ReturnsSignIn()
    {
        var serviceMock = new Mock<IUserRegistrationService>();
        var userManagerMock = CreateUserManagerMock();

        serviceMock
            .Setup(s => s.VerifyTwoFactorAsync(1, "123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Verify2faResult(true, 1));

        var user = new User { Id = 1, Email = "user@example.com", UserName = "user@example.com" };
        userManagerMock.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        userManagerMock.Setup(m => m.GetUserIdAsync(user)).ReturnsAsync("1");
        userManagerMock.Setup(m => m.GetEmailAsync(user)).ReturnsAsync("user@example.com");
        userManagerMock.Setup(m => m.GetUserNameAsync(user)).ReturnsAsync("user@example.com");
        userManagerMock.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(["Owner"]);

        var controller = CreateController(serviceMock, userManagerMock);

        var result = await controller.VerifyTwoFactor(new Verify2faRequest(1, "123456"), CancellationToken.None);

        // SignIn(principal, scheme) returns a SignInResult which produces the token response
        Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
    }

    [Fact]
    public async Task VerifyTwoFactor_InvalidOtp_Returns400()
    {
        var serviceMock = new Mock<IUserRegistrationService>();
        serviceMock
            .Setup(s => s.VerifyTwoFactorAsync(1, "000000", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Verify2faResult(false, 1));

        var controller = CreateController(serviceMock);

        var result = await controller.VerifyTwoFactor(new Verify2faRequest(1, "000000"), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
    }

    [Fact]
    public async Task VerifyTwoFactor_UserNotFoundAfterVerification_Returns400()
    {
        var serviceMock = new Mock<IUserRegistrationService>();
        var userManagerMock = CreateUserManagerMock();

        serviceMock
            .Setup(s => s.VerifyTwoFactorAsync(99, "123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Verify2faResult(true, 99));

        userManagerMock.Setup(m => m.FindByIdAsync("99")).ReturnsAsync((User?)null);

        var controller = CreateController(serviceMock, userManagerMock);

        var result = await controller.VerifyTwoFactor(new Verify2faRequest(99, "123456"), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
    }
}
