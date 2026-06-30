using NMT_api.Services.Translation.Core;

namespace NMT_api.Services.Translation.Jobs;

public interface ITranslationJobService
{
    Task<TranslationJob> CreateJobAsync(
        TranslationJobKind kind,
        string sourceText,
        TranslationRequestOptions options,
        string? fileName,
        CancellationToken cancellationToken);

    TranslationJob? GetJob(Guid jobId);
    IReadOnlyCollection<TranslationJob> GetRecentJobs(int take);
    TranslationRating RateJob(Guid jobId, int score, string? comment, string? ratedBy);
}
