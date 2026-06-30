namespace NMT_api.Contracts.Responses;

public sealed class TranslationRatingResponse
{
    public int Score { get; set; }
    public string? Comment { get; set; }
    public string? RatedBy { get; set; }
    public DateTimeOffset RatedAt { get; set; }
}
