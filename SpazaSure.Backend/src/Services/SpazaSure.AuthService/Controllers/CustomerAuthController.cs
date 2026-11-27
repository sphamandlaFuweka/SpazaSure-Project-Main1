using Microsoft.AspNetCore.Mvc;
using SpazaSure.AuthService.DTOs;
using SpazaSure.AuthService.Services;
using SpazaSure.Shared.Models;

namespace SpazaSure.AuthService.Controllers;

[ApiController]
[Route("api/customer/auth")]
public class CustomerAuthController(CustomerAuthService customerAuth, PhoneVerificationService phoneVerification) : ControllerBase
{
    private string IpAddress => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>Register a new customer account using a phone number and password.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register(CustomerRegisterRequest req)
    {
        var (success, error, data) = await customerAuth.RegisterAsync(req, IpAddress);
        if (!success) return BadRequest(ApiResponse.Fail(error!));
        return Ok(ApiResponse<CustomerAuthResponse>.Ok(data!));
    }

    [HttpGet("phone-verification/status")]
    public IActionResult PhoneVerificationStatus() =>
        Ok(ApiResponse<object>.Ok(new { enabled = phoneVerification.IsEnabled }));

    [HttpPost("phone-verification/send-code")]
    public async Task<IActionResult> SendPhoneVerificationCode(SendPhoneVerificationCodeRequest req)
    {
        var result = await phoneVerification.SendCodeAsync(req.Phone, HttpContext.RequestAborted);
        if (!result.Success) return StatusCode(result.StatusCode, ApiResponse.Fail(result.Message));
        return Ok(ApiResponse.Ok(result.Message));
    }

    /// <summary>Sign in to an existing customer account using phone and password.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(CustomerLoginRequest req)
    {
        var (success, error, data) = await customerAuth.LoginAsync(req, IpAddress);
        if (!success) return Unauthorized(ApiResponse.Fail(error!));
        return Ok(ApiResponse<CustomerAuthResponse>.Ok(data!));
    }
}
