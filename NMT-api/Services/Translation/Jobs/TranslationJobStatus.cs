namespace NMT_api.Services.Translation.Jobs;

public enum TranslationJobStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Canceled
}
