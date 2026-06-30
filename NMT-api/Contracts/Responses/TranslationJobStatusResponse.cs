using NMT_api.Services.Translation.Jobs;

namespace NMT_api.Contracts.Responses;

public sealed class TranslationJobStatusResponse
{
    public Guid JobId { get; set; }
    public TranslationJobKind Kind { get; set; }
    public TranslationJobStatus Status { get; set; }
    public string SourceLanguage { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public int Percent { get; set; }
    public int ProcessedUnits { get; set; }
    public int TotalUnits { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? Device { get; set; }
    public int? BackendDurationMs { get; set; }
    public int? ChunkCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public bool HasResult { get; set; }
    public string? ResultUrl { get; set; }
    public TranslationRatingResponse? Rating { get; set; }
}
