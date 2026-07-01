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
    private readonly IApiTokenService _apiTokenService;
    private readonly ApiTokenOptions _options;

    public AuthController(
        IApiTokenService apiTokenService,
        IOptions<ApiTokenOptions> options)
    {
        _apiTokenService = apiTokenService;
        _options = options.Value;
    }

    [AllowAnonymous]
    [HttpPost("token")]
    public ActionResult<ApiTokenResponse> CreateToken()
    {
        ApiTokenIssueResult token = _apiTokenService.Issue();
        Response.Cookies.Append(_options.CookieName, token.Token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = token.ExpiresAt,
            IsEssential = true,
            Path = "/"
        });

        return Ok(new ApiTokenResponse
        {
            ExpiresAt = token.ExpiresAt,
            ExpiresInSeconds = token.ExpiresInSeconds,
            Message = "API token cookie issued. Same-origin API calls are authenticated automatically until expiration."
        });
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(_options.CookieName, new CookieOptions
        {
            Path = "/"
        });

        return NoContent();
    }
}
