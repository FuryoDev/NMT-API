using Microsoft.AspNetCore.Mvc;
using NMT_api.Contracts.Requests;
using NMT_api.Contracts.Responses;
using NMT_api.Services.Translation.Core;
using NMT_api.Services.Translation.Jobs;
using NMT_api.Services.Translation.Language;
using NMT_api.Services.Translation.Onnx;

namespace NMT_api.Controllers;

[ApiController]
[Route("api/translations")]
public sealed class TranslationsController : ControllerBase
{
    private readonly INmtTranslationService _translationService;
    private readonly ITranslationJobService _jobService;
    private readonly ITranslationLanguageService _languageService;
    private readonly IOnnxNllbRunner _runner;

    public TranslationsController(
        INmtTranslationService translationService,
        ITranslationJobService jobService,
        ITranslationLanguageService languageService,
        IOnnxNllbRunner runner)
    {
        _translationService = translationService;
        _jobService = jobService;
        _languageService = languageService;
        _runner = runner;
    }

    [HttpGet("languages")]
    public ActionResult<IReadOnlyCollection<SupportedLanguageResponse>> GetSupportedLanguages()
    {
        SupportedLanguageResponse[] languages = _languageService.GetSupportedLanguages()
            .Select(language => new SupportedLanguageResponse(language.Code, language.NllbCode))
            .ToArray();

        return Ok(languages);
    }

    [HttpGet("onnx/model")]
    public ActionResult<OnnxModelInfoResponse> GetOnnxModel()
    {
        OnnxModelInfo info = _runner.ModelInfo;

        return Ok(new OnnxModelInfoResponse
        {
            Provider = info.Provider,
            ModelPath = info.ModelPath,
            IsLoaded = info.IsLoaded,
            LoadedAt = info.LoadedAt,
            StartupMs = info.StartupMs,
            InputNames = info.InputNames,
            OutputNames = info.OutputNames
        });
    }

    [HttpPost("text")]
    public async Task<ActionResult<TranslationResponse>> TranslateText(
        [FromBody] TranslateTextRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            TranslationResult result = await _translationService.TranslateTextAsync(
                request.Text,
                BuildOptions(request.SourceLanguage, request.TargetLanguage, request.MaxNewTokens),
                cancellationToken);

            return Ok(MapTranslationResponse(result));
        }
        catch (UnsupportedLanguageException ex)
        {
            return BadRequest(new { error = ex.Message, ex.LanguageCode, ex.SupportedLanguages });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("jobs/text")]
    public async Task<ActionResult<TranslationJobAcceptedResponse>> CreateTextJob(
        [FromBody] TranslateTextRequest request,
        CancellationToken cancellationToken)
    {
        return await CreateJobAsync(
            TranslationJobKind.Text,
            request.Text,
            request.SourceLanguage,
            request.TargetLanguage,
            request.MaxNewTokens,
            fileName: null,
            cancellationToken);
    }

    [HttpPost("jobs/file")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<TranslationJobAcceptedResponse>> CreateFileJob(
        [FromForm] TranslateFileRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null)
        {
            return BadRequest(new { error = "A file is required." });
        }

        string text = await ReadFormFileAsUtf8Async(request.File, cancellationToken);

        return await CreateJobAsync(
            TranslationJobKind.File,
            text,
            request.SourceLanguage,
            request.TargetLanguage,
            request.MaxNewTokens,
            request.File.FileName,
            cancellationToken);
    }

    [HttpPost("jobs/srt")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<TranslationJobAcceptedResponse>> CreateSrtJob(
        [FromForm] TranslateSrtRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null)
        {
            return BadRequest(new { error = "A SRT file is required." });
        }

        string text = await ReadFormFileAsUtf8Async(request.File, cancellationToken);

        return await CreateJobAsync(
            TranslationJobKind.Srt,
            text,
            request.SourceLanguage,
            request.TargetLanguage,
            request.MaxNewTokens,
            request.File.FileName,
            cancellationToken);
    }

    [HttpGet("jobs")]
    public ActionResult<IReadOnlyCollection<TranslationJobStatusResponse>> GetJobs([FromQuery] int take = 50)
    {
        return Ok(_jobService.GetRecentJobs(take).Select(MapJobResponse).ToArray());
    }

    [HttpGet("jobs/{jobId:guid}")]
    public ActionResult<TranslationJobStatusResponse> GetJob(Guid jobId)
    {
        TranslationJob? job = _jobService.GetJob(jobId);
        return job is null ? NotFound() : Ok(MapJobResponse(job));
    }

    [HttpGet("jobs/{jobId:guid}/result")]
    public IActionResult GetJobResult(Guid jobId)
    {
        TranslationJob? job = _jobService.GetJob(jobId);
        if (job is null)
        {
            return NotFound();
        }

        if (job.Status != TranslationJobStatus.Succeeded || job.ResultText is null)
        {
            return Conflict(new { error = "Translation result is not available yet.", job.Status });
        }

        string contentType = job.Kind == TranslationJobKind.Srt ? "application/x-subrip" : "text/plain";
        string fileName = BuildResultFileName(job);
        return File(System.Text.Encoding.UTF8.GetBytes(job.ResultText), contentType, fileName);
    }

    [HttpPost("jobs/{jobId:guid}/rating")]
    public ActionResult<TranslationRatingResponse> RateJob(Guid jobId, [FromBody] RateTranslationRequest request)
    {
        try
        {
            TranslationRating rating = _jobService.RateJob(jobId, request.Score, request.Comment, request.RatedBy);
            return Ok(MapRatingResponse(rating));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    private async Task<ActionResult<TranslationJobAcceptedResponse>> CreateJobAsync(
        TranslationJobKind kind,
        string text,
        string sourceLanguage,
        string targetLanguage,
        int maxNewTokens,
        string? fileName,
        CancellationToken cancellationToken)
    {
        try
        {
            TranslationJob job = await _jobService.CreateJobAsync(
                kind,
                text,
                BuildOptions(sourceLanguage, targetLanguage, maxNewTokens),
                fileName,
                cancellationToken);

            return AcceptedAtAction(nameof(GetJob), new { jobId = job.Id }, MapAcceptedResponse(job));
        }
        catch (UnsupportedLanguageException ex)
        {
            return BadRequest(new { error = ex.Message, ex.LanguageCode, ex.SupportedLanguages });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static TranslationRequestOptions BuildOptions(
        string sourceLanguage,
        string targetLanguage,
        int maxNewTokens)
    {
        return new TranslationRequestOptions(sourceLanguage, targetLanguage, maxNewTokens);
    }

    private static async Task<string> ReadFormFileAsUtf8Async(IFormFile file, CancellationToken cancellationToken)
    {
        await using Stream stream = file.OpenReadStream();
        using StreamReader reader = new(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private TranslationJobAcceptedResponse MapAcceptedResponse(TranslationJob job)
    {
        return new TranslationJobAcceptedResponse
        {
            JobId = job.Id,
            Status = job.Status,
            StatusUrl = Url.ActionLink(nameof(GetJob), values: new { jobId = job.Id }) ?? string.Empty,
            ResultUrl = Url.ActionLink(nameof(GetJobResult), values: new { jobId = job.Id }) ?? string.Empty
        };
    }

    private TranslationJobStatusResponse MapJobResponse(TranslationJob job)
    {
        return new TranslationJobStatusResponse
        {
            JobId = job.Id,
            Kind = job.Kind,
            Status = job.Status,
            SourceLanguage = job.SourceLanguage,
            TargetLanguage = job.TargetLanguage,
            FileName = job.FileName,
            Percent = job.Percent,
            ProcessedUnits = job.ProcessedUnits,
            TotalUnits = job.TotalUnits,
            CurrentStep = job.CurrentStep,
            ErrorMessage = job.ErrorMessage,
            Device = job.Device,
            BackendDurationMs = job.BackendDurationMs,
            ChunkCount = job.ChunkCount,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            HasResult = job.ResultText is not null,
            ResultUrl = job.ResultText is not null
                ? Url.ActionLink(nameof(GetJobResult), values: new { jobId = job.Id })
                : null,
            Rating = job.Rating is null ? null : MapRatingResponse(job.Rating)
        };
    }

    private static TranslationResponse MapTranslationResponse(TranslationResult result)
    {
        return new TranslationResponse
        {
            TranslatedText = result.TranslatedText,
            SourceLanguage = result.SourceLanguage,
            TargetLanguage = result.TargetLanguage,
            SourceNllbLanguage = result.SourceNllbLanguage,
            TargetNllbLanguage = result.TargetNllbLanguage,
            Device = result.Device,
            DurationMs = result.DurationMs,
            ChunkCount = result.ChunkCount
        };
    }

    private static TranslationRatingResponse MapRatingResponse(TranslationRating rating)
    {
        return new TranslationRatingResponse
        {
            Score = rating.Score,
            Comment = rating.Comment,
            RatedBy = rating.RatedBy,
            RatedAt = rating.RatedAt
        };
    }

    private static string BuildResultFileName(TranslationJob job)
    {
        string extension = job.Kind == TranslationJobKind.Srt ? ".srt" : ".txt";
        string stem = string.IsNullOrWhiteSpace(job.FileName)
            ? $"translation-{job.Id:N}"
            : Path.GetFileNameWithoutExtension(job.FileName);

        return $"{stem}.{job.TargetLanguage}{extension}";
    }
}
