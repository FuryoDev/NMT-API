namespace NMT_api.Services.Translation.Jobs;

public sealed class TranslationJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public TranslationJobKind Kind { get; set; }
    public TranslationJobStatus Status { get; set; } = TranslationJobStatus.Queued;
    public string SourceLanguage { get; set; } = "fr";
    public string TargetLanguage { get; set; } = "en";
    public int MaxNewTokens { get; set; } = 512;
    public string? FileName { get; set; }
    public string SourceText { get; set; } = string.Empty;
    public string? ResultText { get; set; }
    public string? ErrorMessage { get; set; }
    public string CurrentStep { get; set; } = "Queued";
    public int Percent { get; set; }
    public int TotalUnits { get; set; } = 1;
    public int ProcessedUnits { get; set; }
    public string? Device { get; set; }
    public int? BackendDurationMs { get; set; }
    public int? ChunkCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public TranslationRating? Rating { get; set; }
}
