namespace NMT_api.Services.Translation.Jobs;

public sealed class TranslationRating
{
    public int Score { get; set; }
    public string? Comment { get; set; }
    public string? RatedBy { get; set; }
    public DateTimeOffset RatedAt { get; set; } = DateTimeOffset.UtcNow;
}
