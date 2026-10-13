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

    /// <summary>Register a new customer account using a phone number and password.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register(CustomerRegisterRequest req)
    {
        var (success, error, data) = await customerAuth.RegisterAsync(req, IpAddress);
        if (!success) return BadRequest(ApiResponse.Fail(error!));
        return Ok(ApiResponse<CustomerAuthResponse>.Ok(data!));
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
