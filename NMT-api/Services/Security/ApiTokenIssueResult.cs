namespace NMT_api.Services.Security;

public sealed record ApiTokenIssueResult(
    string Token,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    int ExpiresInSeconds);
