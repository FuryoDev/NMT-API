using Microsoft.AspNetCore.Mvc;
using NMT_api.Contracts.Requests;
using NMT_api.Models.Translation;
using NMT_api.Services.Srt;
using NMT_api.Services.Translation;

namespace NMT_api.Controllers;

[ApiController]
[Route("")]
public class TranslationController(
    INmtTranslationService translator,
    ISrtTranslationService srtTranslationService) : ControllerBase
{
    [HttpGet]
    public IActionResult Root()
    {
        return Ok(new
        {
            message = "NLLB Translator API is running",
            health = "/health",
            translate = "/translate",
            translateFile = "/translate/file",
            translateFileJson = "/translate/file/json",
            translateSrt = "/translate/srt"
        });
    }

    [HttpGet("health")]
    public ActionResult<HealthResponse> Health()
    {
        return Ok(new HealthResponse
        {
            Status = "ok",
            Device = translator.Device,
            StartupMs = translator.StartupMs,
            Model = translator.ModelName
        });
    }

    [HttpPost("translate")]
    public ActionResult<TranslateResponse> Translate([FromBody] TranslateRequest request)
    {
        string text = (request.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest("Text is required.");
        }

        (string normalized, List<string> appliedRules) = request.Preprocess
            ? TextPreprocessor.Normalize(text)
            : (text, []);

        TranslationResult translation = translator.Translate(
            normalized,
            request.SourceLanguage,
            request.TargetLanguage,
            request.MaxNewTokens,
            request.NumBeams);

        return Ok(new TranslateResponse
        {
            TranslatedText = translation.TranslatedText,
            Device = translation.Device,
            DurationMs = translation.DurationMs,
            Preprocessing = request.Preprocess ? appliedRules : null
        });
    }

    [HttpPost("translate/file")]
    [Consumes("multipart/form-data")]
    [Produces("text/plain")]
    public async Task<IActionResult> TranslateFile([FromForm] TranslateFileRequest request)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        using StreamReader reader = new(request.File.OpenReadStream());
        string input = await reader.ReadToEndAsync();

        (string normalized, _) = request.Preprocess ? TextPreprocessor.Normalize(input) : (input, []);

        TranslationResult translation = translator.Translate(
            normalized,
            request.SourceLanguage,
            request.TargetLanguage,
            request.MaxNewTokens,
            request.NumBeams);

        return Content(translation.TranslatedText, "text/plain");
    }

    [HttpPost("translate/file/json")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<TranslateResponse>> TranslateFileJson([FromForm] TranslateFileJsonRequest request)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        using StreamReader reader = new(request.File.OpenReadStream());
        string input = await reader.ReadToEndAsync();

        (string normalized, List<string> appliedRules) = request.Preprocess
            ? TextPreprocessor.Normalize(input)
            : (input, []);

        TranslationResult translation = translator.Translate(
            normalized,
            request.SourceLanguage,
            request.TargetLanguage,
            request.MaxNewTokens,
            request.NumBeams);

        return Ok(new TranslateResponse
        {
            TranslatedText = translation.TranslatedText,
            Device = translation.Device,
            DurationMs = translation.DurationMs,
            Preprocessing = request.Preprocess ? appliedRules : null
        });
    }

    [HttpPost("translate/srt")]
    [Consumes("multipart/form-data")]
    [Produces("text/plain")]
    public async Task<IActionResult> TranslateSrt([FromForm] TranslateSrtRequest request)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        using StreamReader reader = new(request.File.OpenReadStream());
        string srtText = await reader.ReadToEndAsync();

        string translatedSrt = srtTranslationService.TranslateSrt(
            srtText,
            request.SourceLanguage,
            request.TargetLanguage,
            request.MaxNewTokens,
            request.NumBeams);

        return Content(translatedSrt, "text/plain");
    }
}
