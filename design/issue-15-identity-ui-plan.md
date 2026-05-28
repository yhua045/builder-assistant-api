# Issue #15: ASP.NET Core Identity UI Integration Plan

## Objective
Shift from custom API-based authentication endpoints to using ASP.NET Core Identity's default UI Razor Pages. Serve the API as a central login broker, which easily allows adding external OAuth providers later, while keeping the PKCE token exchange flow intact for clients like mobile apps.

## Key Requirements & Architectural Changes

### 1. Remove Custom Login APIs
- **Remove** `POST /api/auth/login` from `AuthController`.
- **Remove** `POST /api/auth/verify-otp` from `AuthController`.
- **Remove** `POST /api/auth/logout` from `AuthController` (Identity UI will handle logout mechanics).
- Clean up any unused OTP generation/validation logic in the `AuthService` that was backing these endpoints.

### 2. Add ASP.NET Core Identity UI & Passwordless Support
- **Add Packages**: Ensure `Microsoft.AspNetCore.Identity.UI` and `Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation` (if needed) are installed in the `Api` project.
- **Update DI container (`Program.cs` & Infrastructure)**:
  - Migrate from standard Identity to UI-enabled Identity: `AddDefaultIdentity<User>(...).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultUI().AddDefaultTokenProviders()`.
  - Add Razor Pages services: `builder.Services.AddRazorPages();`.
- **Update Middleware Pipeline (`Program.cs`)**:
  - Add `app.MapRazorPages();` to route to the Identity Pages.
  - Ensure `app.UseAuthentication()` and `app.UseAuthorization()` are correctly sequenced.
- **Scaffold Login Page**: Scaffold the `Account/Login` Razor Page (`Areas/Identity/Pages/Account/Login.cshtml`) so its UI and backing C# logic can be modified.
- **Modify Login Logic**: Update the Login form to support sending an OTP (Passwordless Login) by looking up the email and calling `UserManager.GenerateUserTokenAsync(..., TokenOptions.DefaultEmailProvider, "PasswordlessLogin")`.
- **Add VerifyOtp Page**: Create a new `Account/VerifyOtp` Razor Page where users enter the sent token.
- **Implement OTP Verification**: In `VerifyOtp.cshtml.cs`, use `UserManager.VerifyUserTokenAsync` and `SignInManager.SignInAsync` to properly authenticate the user.

### 3. Modify Authorization Endpoint (`GET /api/auth/authorize`)
- **Enforce Authentication**: Instead of rejecting unauthenticated requests with a 401/400 (if cookie is missing), issue a standard authentication `Challenge` (typically a `302 Found` redirecting to the Identity UI Login page).
- **ReturnUrl Handling**: The `Challenge` must capture the original URL (including the PKCE query parameters: `client_id`, `response_type`, `redirect_uri`, `code_challenge`, `code_challenge_method`, `state`) as the `ReturnUrl`. This ensures the Passwordless/Identity UI login flow natively integrates with the `/authorize` endpoint redirection. Once the user completes the OTP flow in Razor Pages, they will be seamlessly redirected *back* to `/authorize`.
- **Code Issuance**: When an authenticated user hits `/authorize`, generate the short-lived `AuthorizationCode`, store it, and `302` redirect to the client's `redirect_uri` with the `code` and `state`.

### 4. Keep Token Endpoint (`POST /api/auth/connect/token`)
- **No fundamental changes**: Continues to act as a JSON API endpoint.
- Validates the `code` and `code_verifier` against the DB, issuing a JWT access token and a refresh token.

## Implementation Steps
1. **Packages**: `dotnet add src/Api package Microsoft.AspNetCore.Identity.UI`
2. **Registration**: Update `src/Api/Program.cs` and `src/Infrastructure/DependencyInjection.cs` to integrate `AddDefaultUI()`, `AddDefaultTokenProviders()`, and `AddRazorPages()`.
3. **Controller Cleanup**: Trim `AuthController.cs` down to just `Authorize` and `ConnectToken`.
4. **Scaffold Identity UI**: Scaffold `Account/Login` and implement the initial submit step to generate the token, then create the `Account/VerifyOtp` page to complete authentication.
5. **Authorize Logic Refactor**: Implement the Challenge / Redirect loop in the `Authorize` endpoint.
6. **Services Clean up**: Remove custom OTP methods in `AuthService` in favor of Identity.
7. **Tests**: Update unit tests in `AuthControllerTests` to reflect the dropped endpoints and the new `Authorize` redirect behavior.