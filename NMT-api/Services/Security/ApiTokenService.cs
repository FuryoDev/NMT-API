using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace NMT_api.Services.Security;

public sealed class ApiTokenService : IApiTokenService
{
    private const string Purpose = "nmt-api-token";
    private readonly IDataProtector _protector;
    private readonly ApiTokenOptions _options;

    public ApiTokenService(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<ApiTokenOptions> options)
    {
        _protector = dataProtectionProvider.CreateProtector("NMT_api.ApiToken.v1");
        _options = options.Value;
    }

    public ApiTokenIssueResult Issue()
    {
        DateTimeOffset issuedAt = DateTimeOffset.UtcNow;
        DateTimeOffset expiresAt = issuedAt.AddMinutes(_options.ExpirationMinutes);
        ApiTokenPayload payload = new(Purpose, issuedAt.ToUnixTimeSeconds(), expiresAt.ToUnixTimeSeconds());
        string protectedPayload = _protector.Protect(JsonSerializer.Serialize(payload));

        return new ApiTokenIssueResult(
            protectedPayload,
            issuedAt,
            expiresAt,
            (int)Math.Round((expiresAt - issuedAt).TotalSeconds));
    }

    public ApiTokenValidationResult Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return ApiTokenValidationResult.Invalid("Missing API token.");
        }

        try
        {
            string json = _protector.Unprotect(token);
            ApiTokenPayload? payload = JsonSerializer.Deserialize<ApiTokenPayload>(json);
            if (payload is null || payload.Purpose != Purpose)
            {
                return ApiTokenValidationResult.Invalid("Invalid API token.");
            }

            DateTimeOffset expiresAt = DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAtUnixSeconds);
            if (expiresAt <= DateTimeOffset.UtcNow)
            {
                return ApiTokenValidationResult.Invalid("API token has expired.");
            }

            return ApiTokenValidationResult.Valid(expiresAt);
        }
        catch
        {
            return ApiTokenValidationResult.Invalid("Invalid API token.");
        }
    }

    private sealed record ApiTokenPayload(
        string Purpose,
        long IssuedAtUnixSeconds,
        long ExpiresAtUnixSeconds);
}
