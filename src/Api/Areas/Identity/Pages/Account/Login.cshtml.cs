using System.ComponentModel.DataAnnotations;
using BuilderAssistantApi.Application.Ports;
using BuilderAssistantApi.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BuilderAssistantApi.Api.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed class LoginModel : PageModel
{
    private readonly UserManager<User> _userManager;
    private readonly IEmailSender _emailSender;

    public LoginModel(UserManager<User> userManager, IEmailSender emailSender)
    {
        _userManager = userManager;
        _emailSender = emailSender;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public sealed class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email address")]
        public string Email { get; set; } = string.Empty;
    }

    public void OnGet()
    {
        // Render the form
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user == null)
        {
            // Return generic message to prevent user enumeration
            ErrorMessage = "If that email is registered, you will receive a login code shortly.";
            return Page();
        }

        var token = await _userManager.GenerateUserTokenAsync(
            user,
            TokenOptions.DefaultEmailProvider,
            "PasswordlessLogin");

        await _emailSender.SendEmailAsync(
            Input.Email,
            "Your Builder Assistant login code",
            $"Your one-time login code is: {token}\n\nThis code expires shortly.");

        return RedirectToPage("VerifyOtp", new { email = Input.Email, ReturnUrl });
    }
}
