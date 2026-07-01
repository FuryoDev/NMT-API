using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace NMT_api.Services.Security;

public sealed class ApiTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IApiTokenService _apiTokenService;
    private readonly ApiTokenOptions _apiTokenOptions;

    public ApiTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiTokenService apiTokenService,
        IOptions<ApiTokenOptions> apiTokenOptions)
        : base(options, logger, encoder)
    {
        _apiTokenService = apiTokenService;
        _apiTokenOptions = apiTokenOptions.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!_apiTokenOptions.Enabled)
        {
            return Task.FromResult(AuthenticateResult.Success(CreateTicket(expiresAt: null)));
        }

        if (!Request.Cookies.TryGetValue(_apiTokenOptions.CookieName, out string? token))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        ApiTokenValidationResult validation = _apiTokenService.Validate(token);
        if (!validation.IsValid)
        {
            return Task.FromResult(AuthenticateResult.Fail(validation.ErrorMessage ?? "Invalid API token."));
        }

        return Task.FromResult(AuthenticateResult.Success(CreateTicket(validation.ExpiresAt)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Response.WriteAsJsonAsync(new
        {
            error = "API token required. Call POST /api/auth/token first; the token is then sent automatically as a cookie."
        });
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Response.WriteAsJsonAsync(new { error = "API token access denied." });
    }

    private AuthenticationTicket CreateTicket(DateTimeOffset? expiresAt)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, "api-token"),
            new(ClaimTypes.Name, "NMT API token")
        ];

        if (expiresAt is not null)
        {
            claims.Add(new Claim("expires_at", expiresAt.Value.ToUnixTimeSeconds().ToString()));
        }

        ClaimsIdentity identity = new(claims, ApiTokenAuthenticationDefaults.AuthenticationScheme);
        ClaimsPrincipal principal = new(identity);
        return new AuthenticationTicket(principal, ApiTokenAuthenticationDefaults.AuthenticationScheme);
    }
}
