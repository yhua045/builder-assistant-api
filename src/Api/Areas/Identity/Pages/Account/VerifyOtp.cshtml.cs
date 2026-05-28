using System.ComponentModel.DataAnnotations;
using BuilderAssistantApi.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BuilderAssistantApi.Api.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed class VerifyOtpModel : PageModel
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;

    public VerifyOtpModel(UserManager<User> userManager, SignInManager<User> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [BindProperty(SupportsGet = true)]
    public string Email { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public sealed class InputModel
    {
        [Required]
        [Display(Name = "Login code")]
        public string Otp { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            return RedirectToPage("Login");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _userManager.FindByEmailAsync(Email);
        if (user == null)
        {
            ErrorMessage = "Invalid or expired login code.";
            return Page();
        }

        var isValid = await _userManager.VerifyUserTokenAsync(
            user,
            TokenOptions.DefaultEmailProvider,
            "PasswordlessLogin",
            Input.Otp);

        if (!isValid)
        {
            ErrorMessage = "Invalid or expired login code. Please try again.";
            return Page();
        }

        await _signInManager.SignInAsync(user, isPersistent: false);

        var returnUrl = ReturnUrl ?? "/";
        if (Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return LocalRedirect("/");
    }
}
