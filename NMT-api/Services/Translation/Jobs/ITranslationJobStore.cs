namespace NMT_api.Services.Translation.Jobs;

public interface ITranslationJobStore
{
    TranslationJob Add(TranslationJob job);
    TranslationJob? Get(Guid jobId);
    IReadOnlyCollection<TranslationJob> GetRecent(int take);
    bool TryUpdate(Guid jobId, Action<TranslationJob> update);
}
