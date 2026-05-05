using System.Security.Claims;
using backend.Entities;
using backend.Models.Auth;
using backend.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace backend.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAntiforgery _antiforgery;
    private readonly AuthService _authService;

    public AuthController(
        IAntiforgery antiforgery,
        AuthService authService)
    {
        _antiforgery = antiforgery;
        _authService = authService;
    }

    [HttpGet("csrf-token")]
    [AllowAnonymous]
    public ActionResult<CsrfTokenResponse> GetCsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new CsrfTokenResponse
        {
            RequestToken = tokens.RequestToken ?? string.Empty
        });
    }

    [HttpGet("status")]
    [AllowAnonymous]
    public ActionResult<AuthUserResponse> Status()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Ok(new AuthUserResponse
            {
                IsAuthenticated = false,
                Username = string.Empty,
                FullName = string.Empty,
                Roles = []
            });
        }

        return Ok(new AuthUserResponse
        {
            IsAuthenticated = true,
            Username = User.Identity?.Name ?? string.Empty,
            FullName = User.FindFirst("full_name")?.Value ?? string.Empty,
            Roles = User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray()
        });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthUserResponse>> Login([FromBody] LoginRequest request)
    {
        await _antiforgery.ValidateRequestAsync(HttpContext);

        var user = await _authService.ValidateUserAsync(request.Username, request.Password);
        if (user is null)
        {
            return Unauthorized(new { message = "Username atau password salah." });
        }

        var claims = AuthService.BuildClaims(user);
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                AllowRefresh = true,
                IsPersistent = false,
                IssuedUtc = DateTimeOffset.UtcNow,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
            });

        return Ok(new AuthUserResponse
        {
            IsAuthenticated = true,
            Username = user.Username,
            FullName = user.FullName,
            Roles = [user.Role]
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _antiforgery.ValidateRequestAsync(HttpContext);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthUserResponse>> Me()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        return Ok(new AuthUserResponse
        {
            IsAuthenticated = true,
            Username = User.Identity?.Name ?? string.Empty,
            FullName = User.FindFirst("full_name")?.Value ?? string.Empty,
            Roles = User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray()
        });
    }
}
