using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NMT_api.Contracts.Responses;
using NMT_api.Services.Security;

namespace NMT_api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ApiTokenOptions _options;

    public AuthController(IOptions<ApiTokenOptions> options)
    {
        _options = options.Value;
    }

    [AllowAnonymous]
    [HttpPost("token")]
    public async Task<ActionResult<ApiTokenResponse>> CreateToken()
    {
        DateTimeOffset issuedAt = DateTimeOffset.UtcNow;
        DateTimeOffset expiresAt = issuedAt.AddMinutes(_options.ExpirationMinutes);

        ClaimsIdentity identity = new(
            [
                new Claim(ClaimTypes.NameIdentifier, "nmt-api-user"),
                new Claim(ClaimTypes.Name, "NMT API User"),
                new Claim("issued_at", issuedAt.ToUnixTimeSeconds().ToString())
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        AuthenticationProperties properties = new()
        {
            IsPersistent = false,
            IssuedUtc = issuedAt,
            ExpiresUtc = expiresAt,
            AllowRefresh = false
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            properties);

        return Ok(new ApiTokenResponse
        {
            ExpiresAt = expiresAt,
            ExpiresInSeconds = (int)Math.Round((expiresAt - issuedAt).TotalSeconds),
            Message = "Authentication cookie issued. Subsequent same-origin API calls are authenticated automatically until expiration."
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
