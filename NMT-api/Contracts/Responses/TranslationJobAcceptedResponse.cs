using NMT_api.Services.Translation.Jobs;

namespace NMT_api.Contracts.Responses;

public sealed class TranslationJobAcceptedResponse
{
    public Guid JobId { get; set; }
    public TranslationJobStatus Status { get; set; }
    public string StatusUrl { get; set; } = string.Empty;
    public string ResultUrl { get; set; } = string.Empty;
}
