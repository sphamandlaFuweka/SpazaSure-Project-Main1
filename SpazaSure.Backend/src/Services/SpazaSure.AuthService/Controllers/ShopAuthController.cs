using Microsoft.AspNetCore.Mvc;
using SpazaSure.AuthService.DTOs;
using SpazaSure.AuthService.Services;
using SpazaSure.Shared.Models;

namespace SpazaSure.AuthService.Controllers;

[ApiController]
[Route("api/shop/auth")]
public class ShopAuthController(ShopAuthService shopAuth) : ControllerBase
{
    private string IpAddress => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>
    /// Step 1 — Send OTP to phone number (works for both login and registration)
    /// </summary>
    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp(SendOtpRequest req, [FromQuery] string purpose = "login")
    {
        if (purpose != "login" && purpose != "registration")
            return BadRequest(ApiResponse.Fail("Purpose must be 'login' or 'registration'."));

        var (success, error, devOtp) = await shopAuth.SendOtpAsync(req.Phone, purpose);
        if (!success) return BadRequest(ApiResponse.Fail(error!));

        // In non-production, include OTP in response for auto-fill
        if (devOtp != null)
            return Ok(ApiResponse<object>.Ok(new { otp = devOtp }, "OTP sent successfully."));

        return Ok(ApiResponse.Ok("OTP sent successfully."));
    }

    /// <summary>
    /// Step 2a — Register new spaza shop (verifies OTP + creates account)
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register(ShopRegisterRequest req)
    {
        var (success, error, data) = await shopAuth.RegisterAsync(req, IpAddress);
        if (!success) return BadRequest(ApiResponse.Fail(error!));
        return Ok(ApiResponse<ShopAuthResponse>.Ok(data!));
    }

    /// <summary>
    /// Step 2b — Login existing spaza shop (verifies OTP)
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(ShopLoginRequest req)
    {
        var (success, error, data) = await shopAuth.LoginAsync(req, IpAddress);
        if (!success) return Unauthorized(ApiResponse.Fail(error!));
        return Ok(ApiResponse<ShopAuthResponse>.Ok(data!));
    }
}
