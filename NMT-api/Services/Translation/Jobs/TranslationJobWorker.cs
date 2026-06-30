using Microsoft.Extensions.Options;
using NMT_api.Services.Translation.Core;
using NMT_api.Services.Translation.Srt;

namespace NMT_api.Services.Translation.Jobs;

public sealed class TranslationJobWorker : BackgroundService
{
    private readonly ITranslationJobQueue _queue;
    private readonly ITranslationJobStore _store;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TranslationDefaultsOptions _defaults;
    private readonly ILogger<TranslationJobWorker> _logger;

    public TranslationJobWorker(
        ITranslationJobQueue queue,
        ITranslationJobStore store,
        IServiceScopeFactory scopeFactory,
        IOptions<TranslationDefaultsOptions> defaults,
        ILogger<TranslationJobWorker> logger)
    {
        _queue = queue;
        _store = store;
        _scopeFactory = scopeFactory;
        _defaults = defaults.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            QueuedTranslationJob queuedJob = await _queue.DequeueAsync(stoppingToken);
            await ProcessJobAsync(queuedJob.JobId, stoppingToken);
        }
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        TranslationJob? job = _store.Get(jobId);
        if (job is null)
        {
            _logger.LogWarning("Translation job {JobId} disappeared before processing.", jobId);
            return;
        }

        try
        {
            MarkRunning(jobId);

            using IServiceScope scope = _scopeFactory.CreateScope();
            INmtTranslationService translationService = scope.ServiceProvider.GetRequiredService<INmtTranslationService>();
            ISrtService srtService = scope.ServiceProvider.GetRequiredService<ISrtService>();

            if (job.Kind == TranslationJobKind.Srt)
            {
                await TranslateSrtJobAsync(job, translationService, srtService, cancellationToken);
            }
            else
            {
                await TranslateSingleTextJobAsync(job, translationService, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _store.TryUpdate(jobId, current =>
            {
                current.Status = TranslationJobStatus.Canceled;
                current.CurrentStep = "Canceled";
                current.CompletedAt = DateTimeOffset.UtcNow;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Translation job {JobId} failed.", jobId);
            _store.TryUpdate(jobId, current =>
            {
                current.Status = TranslationJobStatus.Failed;
                current.CurrentStep = "Failed";
                current.ErrorMessage = ex.Message;
                current.CompletedAt = DateTimeOffset.UtcNow;
            });
        }
    }

    private void MarkRunning(Guid jobId)
    {
        _store.TryUpdate(jobId, job =>
        {
            job.Status = TranslationJobStatus.Running;
            job.StartedAt = DateTimeOffset.UtcNow;
            job.CurrentStep = "Starting translation";
            job.Percent = 1;
        });
    }

    private async Task TranslateSingleTextJobAsync(
        TranslationJob job,
        INmtTranslationService translationService,
        CancellationToken cancellationToken)
    {
        _store.TryUpdate(job.Id, current =>
        {
            current.CurrentStep = "Calling translation backend";
            current.Percent = 10;
            current.TotalUnits = 1;
            current.ProcessedUnits = 0;
        });

        TranslationResult result = await translationService.TranslateTextAsync(
            job.SourceText,
            BuildOptions(job),
            cancellationToken);

        _store.TryUpdate(job.Id, current =>
        {
            current.Status = TranslationJobStatus.Succeeded;
            current.CurrentStep = "Completed";
            current.Percent = 100;
            current.ProcessedUnits = 1;
            current.ResultText = result.TranslatedText;
            current.Device = result.Device;
            current.BackendDurationMs = result.DurationMs;
            current.ChunkCount = result.ChunkCount;
            current.CompletedAt = DateTimeOffset.UtcNow;
        });
    }

    private async Task TranslateSrtJobAsync(
        TranslationJob job,
        INmtTranslationService translationService,
        ISrtService srtService,
        CancellationToken cancellationToken)
    {
        SrtDocument document = srtService.Parse(job.SourceText);

        if (document.Blocks.Count == 0)
        {
            throw new ArgumentException("The SRT file does not contain any translatable subtitle block.");
        }

        _store.TryUpdate(job.Id, current =>
        {
            current.CurrentStep = "SRT parsed";
            current.TotalUnits = document.Blocks.Count;
            current.ProcessedUnits = 0;
            current.Percent = 5;
        });

        List<SrtBlock> translatedBlocks = [];
        int processed = 0;
        int totalBackendDurationMs = 0;
        string? device = null;

        foreach (SrtBlock block in document.Blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string blockText = srtService.JoinBlockText(block);
            IReadOnlyList<string> translatedLines = [];

            if (!string.IsNullOrWhiteSpace(blockText))
            {
                TranslationResult result = await translationService.TranslateTextAsync(
                    blockText,
                    BuildOptions(job),
                    cancellationToken);

                translatedLines = srtService.SplitTextBackToLines(result.TranslatedText, _defaults.SrtMaxLineLength);
                totalBackendDurationMs += result.DurationMs;
                device ??= result.Device;
            }

            translatedBlocks.Add(block with { Lines = translatedLines });
            processed++;

            int percent = 5 + (int)Math.Round(processed * 90d / document.Blocks.Count);
            _store.TryUpdate(job.Id, current =>
            {
                current.CurrentStep = $"Translated subtitle block {processed}/{document.Blocks.Count}";
                current.ProcessedUnits = processed;
                current.Percent = Math.Min(percent, 95);
                current.Device = device;
                current.BackendDurationMs = totalBackendDurationMs;
            });
        }

        string translatedSrt = srtService.Build(new SrtDocument(translatedBlocks));

        _store.TryUpdate(job.Id, current =>
        {
            current.Status = TranslationJobStatus.Succeeded;
            current.CurrentStep = "Completed";
            current.Percent = 100;
            current.ResultText = translatedSrt;
            current.CompletedAt = DateTimeOffset.UtcNow;
        });
    }

    private static TranslationRequestOptions BuildOptions(TranslationJob job)
    {
        return new TranslationRequestOptions(
            job.SourceLanguage,
            job.TargetLanguage,
            job.MaxNewTokens);
    }
}
