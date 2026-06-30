using NMT_api.Services.Translation.Core;
using NMT_api.Services.Translation.Language;

namespace NMT_api.Services.Translation.Jobs;

public sealed class TranslationJobService : ITranslationJobService
{
    private readonly ITranslationJobStore _store;
    private readonly ITranslationJobQueue _queue;
    private readonly ITranslationLanguageService _languageService;

    public TranslationJobService(
        ITranslationJobStore store,
        ITranslationJobQueue queue,
        ITranslationLanguageService languageService)
    {
        _store = store;
        _queue = queue;
        _languageService = languageService;
    }

    public async Task<TranslationJob> CreateJobAsync(
        TranslationJobKind kind,
        string sourceText,
        TranslationRequestOptions options,
        string? fileName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            throw new ArgumentException("The text to translate cannot be empty.", nameof(sourceText));
        }

        _languageService.Resolve(options.SourceLanguage);
        _languageService.Resolve(options.TargetLanguage);

        TranslationJob job = new()
        {
            Kind = kind,
            SourceText = sourceText,
            SourceLanguage = options.SourceLanguage,
            TargetLanguage = options.TargetLanguage,
            MaxNewTokens = options.MaxNewTokens,
            FileName = fileName,
            CurrentStep = "Waiting in queue",
            TotalUnits = kind == TranslationJobKind.Srt ? 0 : 1
        };

        _store.Add(job);
        await _queue.QueueAsync(new QueuedTranslationJob(job.Id), cancellationToken);

        return job;
    }

    public TranslationJob? GetJob(Guid jobId)
    {
        return _store.Get(jobId);
    }

    public IReadOnlyCollection<TranslationJob> GetRecentJobs(int take)
    {
        return _store.GetRecent(take);
    }

    public TranslationRating RateJob(Guid jobId, int score, string? comment, string? ratedBy)
    {
        if (score is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be between 1 and 5.");
        }

        TranslationRating rating = new()
        {
            Score = score,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            RatedBy = string.IsNullOrWhiteSpace(ratedBy) ? null : ratedBy.Trim(),
            RatedAt = DateTimeOffset.UtcNow
        };

        bool updated = _store.TryUpdate(jobId, job =>
        {
            if (job.Status != TranslationJobStatus.Succeeded)
            {
                throw new InvalidOperationException("Only a successful translation can be rated.");
            }

            job.Rating = rating;
        });

        if (!updated)
        {
            throw new KeyNotFoundException($"Translation job '{jobId}' was not found.");
        }

        return rating;
    }
}
