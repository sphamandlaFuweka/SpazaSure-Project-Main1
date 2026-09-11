using Microsoft.AspNetCore.Mvc;
using SpazaSure.AuthService.DTOs;
using SpazaSure.AuthService.Services;
using SpazaSure.Shared.Models;

namespace SpazaSure.AuthService.Controllers;

[ApiController]
[Route("api/customer/auth")]
public class CustomerAuthController(CustomerAuthService customerAuth) : ControllerBase
{
    private string IpAddress => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>Step 1 — Send OTP to phone number (works for both login and registration)</summary>
    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp(SendOtpRequest req, [FromQuery] string purpose = "login")
    {
        if (purpose != "login" && purpose != "registration")
            return BadRequest(ApiResponse.Fail("Purpose must be 'login' or 'registration'."));

        var (success, error, devOtp) = await customerAuth.SendOtpAsync(req.Phone, purpose);
        if (!success) return BadRequest(ApiResponse.Fail(error!));

        if (devOtp != null)
            return Ok(ApiResponse<object>.Ok(new { otp = devOtp }, "OTP sent successfully."));

        return Ok(ApiResponse.Ok("OTP sent successfully."));
    }

    /// <summary>Step 2a — Register a new customer account (verifies OTP + creates account)</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register(CustomerRegisterRequest req)
    {
        var (success, error, data) = await customerAuth.RegisterAsync(req, IpAddress);
        if (!success) return BadRequest(ApiResponse.Fail(error!));
        return Ok(ApiResponse<CustomerAuthResponse>.Ok(data!));
    }

    /// <summary>Step 2b — Login an existing customer account (verifies OTP)</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(CustomerLoginRequest req)
    {
        var (success, error, data) = await customerAuth.LoginAsync(req, IpAddress);
        if (!success) return Unauthorized(ApiResponse.Fail(error!));
        return Ok(ApiResponse<CustomerAuthResponse>.Ok(data!));
    }
}
