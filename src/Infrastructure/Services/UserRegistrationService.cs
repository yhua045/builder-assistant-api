using BuilderAssistantApi.Application.Ports;
using BuilderAssistantApi.Application.Services;
using BuilderAssistantApi.Domain.Constants;
using BuilderAssistantApi.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace BuilderAssistantApi.Infrastructure.Services;

public sealed class UserRegistrationService : IUserRegistrationService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole<long>> _roleManager;
    private readonly IEmailSender _emailSender;

    public UserRegistrationService(
        UserManager<User> userManager,
        RoleManager<IdentityRole<long>> roleManager,
        IEmailSender emailSender)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _emailSender = emailSender;
    }

    public async Task<RegistrationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
        {
            return new RegistrationResult(false, null, ["Email is already registered."]);
        }

        var user = new User
        {
            Email = request.Email,
            UserName = request.Email,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return new RegistrationResult(false, null, createResult.Errors.Select(e => e.Description));
        }

        if (!await _roleManager.RoleExistsAsync(ApplicationRoles.Owner))
        {
            await _roleManager.CreateAsync(new IdentityRole<long>(ApplicationRoles.Owner));
        }

        await _userManager.AddToRoleAsync(user, ApplicationRoles.Owner);

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var subject = "Confirm your email";
        var body = $"Email confirmation token: {token}";
        await _emailSender.SendEmailAsync(user.Email!, subject, body, cancellationToken);

        return new RegistrationResult(true, new RegisterResponse(user.Id, user.Email!), []);
    }

    public async Task<ConfirmEmailResult> ConfirmEmailAsync(long userId, string token, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new ConfirmEmailResult(false, ["User not found."]);
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            return new ConfirmEmailResult(false, result.Errors.Select(e => e.Description));
        }

        return new ConfirmEmailResult(true, []);
    }

    public async Task<Verify2faResult> VerifyTwoFactorAsync(long userId, string token, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new Verify2faResult(false, 0);
        }

        var isValid = await _userManager.VerifyTwoFactorTokenAsync(user, "Email", token);
        return new Verify2faResult(isValid, userId);
    }

    public async Task SendTwoFactorCodeAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return;
        }

        var otp = await _userManager.GenerateTwoFactorTokenAsync(user, "Email");
        var subject = "Your sign-in code";
        var body = $"Your two-factor authentication code is: {otp}";
        await _emailSender.SendEmailAsync(user.Email!, subject, body, cancellationToken);
    }
}
