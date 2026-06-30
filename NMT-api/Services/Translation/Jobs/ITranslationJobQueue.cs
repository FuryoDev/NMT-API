namespace NMT_api.Services.Translation.Jobs;

public interface ITranslationJobQueue
{
    ValueTask QueueAsync(QueuedTranslationJob job, CancellationToken cancellationToken);
    ValueTask<QueuedTranslationJob> DequeueAsync(CancellationToken cancellationToken);
}
