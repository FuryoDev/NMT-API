namespace NMT_api.Services.Security;

public sealed class ApiTokenOptions
{
    public bool Enabled { get; set; } = true;
    public int ExpirationMinutes { get; set; } = 5;
    public string CookieName { get; set; } = "nmt_api_token";
}
