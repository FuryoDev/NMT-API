namespace NMT_api.Services.Security;

public sealed record ApiTokenValidationResult(
    bool IsValid,
    DateTimeOffset? ExpiresAt,
    string? ErrorMessage)
{
    public static ApiTokenValidationResult Valid(DateTimeOffset expiresAt)
    {
        return new ApiTokenValidationResult(true, expiresAt, ErrorMessage: null);
    }

    public static ApiTokenValidationResult Invalid(string errorMessage)
    {
        return new ApiTokenValidationResult(false, ExpiresAt: null, ErrorMessage: errorMessage);
    }
}
