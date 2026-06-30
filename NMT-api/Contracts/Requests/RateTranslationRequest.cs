namespace NMT_api.Contracts.Requests;

public sealed class RateTranslationRequest
{
    public int Score { get; set; }
    public string? Comment { get; set; }
    public string? RatedBy { get; set; }
}
