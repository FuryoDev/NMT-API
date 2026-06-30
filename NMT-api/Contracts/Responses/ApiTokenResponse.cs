namespace NMT_api.Contracts.Responses;

public sealed class ApiTokenResponse
{
    public string TokenType { get; set; } = "Cookie";
    public DateTimeOffset ExpiresAt { get; set; }
    public int ExpiresInSeconds { get; set; }
    public string Message { get; set; } = string.Empty;
}
