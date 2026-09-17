using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sompriti.Erp.Application.Auth;

namespace Sompriti.Erp.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("register"), AllowAnonymous, EnableRateLimiting("auth")]
    public Task<AuthResponse> Register(RegisterRequest request, CancellationToken ct) => auth.RegisterAsync(request, ct);

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("auth")]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);

    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("refresh")]
    public Task<AuthResponse> Refresh(RefreshRequest request, CancellationToken ct) => auth.RefreshAsync(request, ct);

    [HttpPost("logout"), AllowAnonymous]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(request, ct);
        return NoContent();
    }

    [HttpPost("forgot-password"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await auth.ForgotPasswordAsync(request, ct);
        return Ok(new { message = "If an account exists for this email, a password reset link has been sent." });
    }

    [HttpPost("reset-password"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await auth.ResetPasswordAsync(request, ct);
        return Ok(new { message = "Your password has been reset. You can now log in." });
    }

    [HttpGet("me"), Authorize]
    public Task<CurrentUserDto> Me(CancellationToken ct) => auth.MeAsync(ct);

    [HttpPost("change-password"), Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(request, ct);
        return Ok(new { message = "Password changed. Please log in again." });
    }
}
