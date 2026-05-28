using BuilderAssistantApi.Application.Ports;
using BuilderAssistantApi.Application.Services;
using BuilderAssistantApi.Domain.Constants;
using BuilderAssistantApi.Domain.Entities;
using BuilderAssistantApi.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace BuilderAssistantApi.Infrastructure.Tests.Services;

public sealed class UserRegistrationServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Mock<UserManager<User>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<User>>();
#pragma warning disable CS8625 // null for optional ctor params that Moq handles
        return new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625
    }

    private static Mock<RoleManager<IdentityRole<long>>> CreateRoleManagerMock()
    {
        var store = new Mock<IRoleStore<IdentityRole<long>>>();
#pragma warning disable CS8625
        return new Mock<RoleManager<IdentityRole<long>>>(store.Object, null, null, null, null);
#pragma warning restore CS8625
    }

    private static UserRegistrationService CreateService(
        Mock<UserManager<User>> userManager,
        Mock<RoleManager<IdentityRole<long>>> roleManager,
        Mock<IEmailSender>? emailSender = null)
    {
        var sender = emailSender ?? new Mock<IEmailSender>();
        return new UserRegistrationService(userManager.Object, roleManager.Object, sender.Object);
    }

    private static User MakeUser(long id = 1, string email = "test@example.com") =>
        new() { Id = id, Email = email, UserName = email };

    // ── RegisterAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_NewEmail_CreatesUserAndAssignsOwnerRole()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();
        var emailSender = new Mock<IEmailSender>();
        var service = CreateService(userManager, roleManager, emailSender);

        userManager.Setup(m => m.FindByEmailAsync("new@example.com")).ReturnsAsync((User?)null);
        userManager.Setup(m => m.CreateAsync(It.IsAny<User>(), "Password123!")).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.AddToRoleAsync(It.IsAny<User>(), ApplicationRoles.Owner)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<User>())).ReturnsAsync("confirm-token");
        roleManager.Setup(m => m.RoleExistsAsync(ApplicationRoles.Owner)).ReturnsAsync(true);
        emailSender.Setup(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);

        var result = await service.RegisterAsync(new RegisterRequest("new@example.com", "Password123!"));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.User);
        Assert.Equal("new@example.com", result.User.Email);
        Assert.Empty(result.Errors);
        userManager.Verify(m => m.AddToRoleAsync(It.IsAny<User>(), ApplicationRoles.Owner), Times.Once);
        emailSender.Verify(s => s.SendEmailAsync(
            "new@example.com",
            It.IsAny<string>(),
            It.Is<string>(b => b.Contains("confirm-token")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ReturnsFailedResult()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();
        var service = CreateService(userManager, roleManager);

        userManager.Setup(m => m.FindByEmailAsync("existing@example.com")).ReturnsAsync(MakeUser(email: "existing@example.com"));

        var result = await service.RegisterAsync(new RegisterRequest("existing@example.com", "Password123!"));

        Assert.False(result.Succeeded);
        Assert.Null(result.User);
        Assert.Single(result.Errors);
        Assert.Contains("already registered", result.Errors.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisterAsync_WeakPassword_ReturnsIdentityErrors()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();
        var service = CreateService(userManager, roleManager);

        var identityErrors = new[]
        {
            new IdentityError { Code = "PasswordTooShort", Description = "Passwords must be at least 6 characters." }
        };

        userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        userManager.Setup(m => m.CreateAsync(It.IsAny<User>(), "weak")).ReturnsAsync(IdentityResult.Failed(identityErrors));

        var result = await service.RegisterAsync(new RegisterRequest("user@example.com", "weak"));

        Assert.False(result.Succeeded);
        Assert.Null(result.User);
        Assert.Contains("Passwords must be at least 6 characters.", result.Errors);
    }

    [Fact]
    public async Task RegisterAsync_DoesNotExposePasswordInResult()
    {
        const string password = "SuperSecret123!";
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();
        var emailSender = new Mock<IEmailSender>();
        var service = CreateService(userManager, roleManager, emailSender);

        userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        userManager.Setup(m => m.CreateAsync(It.IsAny<User>(), password)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.AddToRoleAsync(It.IsAny<User>(), ApplicationRoles.Owner)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<User>())).ReturnsAsync("token");
        roleManager.Setup(m => m.RoleExistsAsync(ApplicationRoles.Owner)).ReturnsAsync(true);
        emailSender.Setup(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);

        var result = await service.RegisterAsync(new RegisterRequest("test@example.com", password));

        // The password must not appear in any part of the returned result
        var resultText = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain(password, resultText);
    }

    // ── ConfirmEmailAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmEmailAsync_ValidToken_ReturnsSuccess()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();
        var service = CreateService(userManager, roleManager);

        var user = MakeUser(id: 1);
        userManager.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        userManager.Setup(m => m.ConfirmEmailAsync(user, "valid-token")).ReturnsAsync(IdentityResult.Success);

        var result = await service.ConfirmEmailAsync(1, "valid-token");

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ConfirmEmailAsync_InvalidToken_ReturnsFailure()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();
        var service = CreateService(userManager, roleManager);

        var user = MakeUser(id: 1);
        userManager.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        userManager.Setup(m => m.ConfirmEmailAsync(user, "bad-token"))
                   .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "InvalidToken", Description = "Invalid token." }));

        var result = await service.ConfirmEmailAsync(1, "bad-token");

        Assert.False(result.Succeeded);
        Assert.Contains("Invalid token.", result.Errors);
    }

    [Fact]
    public async Task ConfirmEmailAsync_UserNotFound_ReturnsFailure()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();
        var service = CreateService(userManager, roleManager);

        userManager.Setup(m => m.FindByIdAsync("99")).ReturnsAsync((User?)null);

        var result = await service.ConfirmEmailAsync(99, "any-token");

        Assert.False(result.Succeeded);
        Assert.Contains("User not found.", result.Errors);
    }

    // ── VerifyTwoFactorAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task VerifyTwoFactorAsync_ValidOtp_ReturnsSuccess()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();
        var service = CreateService(userManager, roleManager);

        var user = MakeUser(id: 1);
        userManager.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        userManager.Setup(m => m.VerifyTwoFactorTokenAsync(user, "Email", "123456")).ReturnsAsync(true);

        var result = await service.VerifyTwoFactorAsync(1, "123456");

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.UserId);
    }

    [Fact]
    public async Task VerifyTwoFactorAsync_InvalidOtp_ReturnsFailure()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();
        var service = CreateService(userManager, roleManager);

        var user = MakeUser(id: 1);
        userManager.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        userManager.Setup(m => m.VerifyTwoFactorTokenAsync(user, "Email", "000000")).ReturnsAsync(false);

        var result = await service.VerifyTwoFactorAsync(1, "000000");

        Assert.False(result.Succeeded);
        Assert.Equal(1, result.UserId);
    }

    [Fact]
    public async Task VerifyTwoFactorAsync_UserNotFound_ReturnsFailure()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();
        var service = CreateService(userManager, roleManager);

        userManager.Setup(m => m.FindByIdAsync("99")).ReturnsAsync((User?)null);

        var result = await service.VerifyTwoFactorAsync(99, "123456");

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.UserId);
    }

    // ── SendTwoFactorCodeAsync ───────────────────────────────────────────────

    [Fact]
    public async Task SendTwoFactorCodeAsync_UserExists_GeneratesOtpAndSendsEmail()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();
        var emailSender = new Mock<IEmailSender>();
        var service = CreateService(userManager, roleManager, emailSender);

        var user = MakeUser(id: 1);
        userManager.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        userManager.Setup(m => m.GenerateTwoFactorTokenAsync(user, "Email")).ReturnsAsync("654321");
        emailSender.Setup(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);

        await service.SendTwoFactorCodeAsync(1);

        emailSender.Verify(s => s.SendEmailAsync(
            "test@example.com",
            It.IsAny<string>(),
            It.Is<string>(b => b.Contains("654321")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendTwoFactorCodeAsync_UserNotFound_DoesNotSendEmail()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();
        var emailSender = new Mock<IEmailSender>();
        var service = CreateService(userManager, roleManager, emailSender);

        userManager.Setup(m => m.FindByIdAsync("99")).ReturnsAsync((User?)null);

        await service.SendTwoFactorCodeAsync(99);

        emailSender.Verify(s => s.SendEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
