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
    /// Register a new spaza shop using a phone number and password.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register(ShopRegisterRequest req)
    {
        var (success, error, data) = await shopAuth.RegisterAsync(req, IpAddress);
        if (!success) return BadRequest(ApiResponse.Fail(error!));
        return Ok(ApiResponse<ShopAuthResponse>.Ok(data!));
    }

    /// <summary>
    /// Sign in to an existing spaza shop using a phone number and password.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(ShopLoginRequest req)
    {
        var (success, error, data) = await shopAuth.LoginAsync(req, IpAddress);
        if (!success) return Unauthorized(ApiResponse.Fail(error!));
        return Ok(ApiResponse<ShopAuthResponse>.Ok(data!));
    }
}
