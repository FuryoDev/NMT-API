using System.Collections.Concurrent;

namespace NMT_api.Services.Translation.Jobs;

public sealed class InMemoryTranslationJobStore : ITranslationJobStore
{
    private readonly ConcurrentDictionary<Guid, TranslationJob> _jobs = [];

    public TranslationJob Add(TranslationJob job)
    {
        _jobs[job.Id] = job;
        return job;
    }

    public TranslationJob? Get(Guid jobId)
    {
        return _jobs.TryGetValue(jobId, out TranslationJob? job) ? job : null;
    }

    public IReadOnlyCollection<TranslationJob> GetRecent(int take)
    {
        return _jobs.Values
            .OrderByDescending(job => job.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToArray();
    }

    public bool TryUpdate(Guid jobId, Action<TranslationJob> update)
    {
        if (!_jobs.TryGetValue(jobId, out TranslationJob? job))
        {
            return false;
        }

        lock (job)
        {
            update(job);
        }

        return true;
    }
}
