using Microsoft.AspNetCore.Mvc;
using SpazaSure.AuthService.DTOs;
using SpazaSure.AuthService.Services;
using SpazaSure.Shared.Models;

namespace SpazaSure.AuthService.Controllers;

[ApiController]
[Route("api/supplier/auth")]
public class AuthController(AuthenticationService authService) : ControllerBase
{
    private string IpAddress => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        var (success, error, data) = await authService.RegisterAsync(req, IpAddress);
        if (!success) return BadRequest(ApiResponse.Fail(error!));
        return Ok(ApiResponse<AuthResponse>.Ok(data!));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var (success, error, data) = await authService.LoginAsync(req, IpAddress);
        if (!success) return Unauthorized(ApiResponse.Fail(error!));
        return Ok(ApiResponse<AuthResponse>.Ok(data!));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest req)
    {
        var (success, error, data) = await authService.RefreshAsync(req.RefreshToken, IpAddress);
        if (!success) return Unauthorized(ApiResponse.Fail(error!));
        return Ok(ApiResponse<AuthResponse>.Ok(data!));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest req)
    {
        await authService.RevokeAsync(req.RefreshToken);
        return Ok(ApiResponse.Ok("Logged out."));
    }

    // Always returns 200 to prevent email enumeration
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest req)
    {
        await authService.ForgotPasswordAsync(req.Email);
        return Ok(ApiResponse.Ok("If that email exists, a reset link has been sent."));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest req)
    {
        var (success, error) = await authService.ResetPasswordAsync(req);
        if (!success) return BadRequest(ApiResponse.Fail(error!));
        return Ok(ApiResponse.Ok("Password reset successfully. Please sign in."));
    }
}
