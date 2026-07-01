namespace NMT_api.Services.Security;

public interface IApiTokenService
{
    ApiTokenIssueResult Issue();
    ApiTokenValidationResult Validate(string token);
}
