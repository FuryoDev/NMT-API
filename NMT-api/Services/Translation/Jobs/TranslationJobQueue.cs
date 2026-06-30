using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace NMT_api.Services.Translation.Jobs;

public sealed class TranslationJobQueue : ITranslationJobQueue
{
    private readonly Channel<QueuedTranslationJob> _queue;

    public TranslationJobQueue(IOptions<TranslationJobQueueOptions> options)
    {
        _queue = Channel.CreateBounded<QueuedTranslationJob>(
            new BoundedChannelOptions(options.Value.Capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
    }

    public ValueTask QueueAsync(QueuedTranslationJob job, CancellationToken cancellationToken)
    {
        return _queue.Writer.WriteAsync(job, cancellationToken);
    }

    public ValueTask<QueuedTranslationJob> DequeueAsync(CancellationToken cancellationToken)
    {
        return _queue.Reader.ReadAsync(cancellationToken);
    }
}
