using Microsoft.AspNetCore.Mvc;
using NMT_api.Contracts.Requests;
using NMT_api.Models.Translation;
using NMT_api.Services.Translation.PythonBridge;

namespace NMT_api.Controllers;

[ApiController]
[Route("")]
public class TranslationController(IPythonTranslationBackendClient pythonBackendClient) : ControllerBase
{
    [HttpGet]
    public IActionResult Root()
    {
        return Ok(new
        {
            message = "NMT .NET gateway is running",
            health = "/health",
            translate = "/translate",
            translateFile = "/translate/file",
            translateFileJson = "/translate/file/json",
            translateSrt = "/translate/srt"
        });
    }

    [HttpGet("health")]
    public async Task<ActionResult<HealthResponse>> Health(CancellationToken cancellationToken)
    {
        PythonBridge.Contracts.PythonHealthResponse? health = await pythonBackendClient.GetHealthAsync(cancellationToken);
        if (health is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Python backend is unreachable.");
        }

        return Ok(new HealthResponse
        {
            Status = health.Status,
            Device = health.Device,
            StartupMs = health.StartupMs,
            Model = health.Model
        });
    }

    [HttpPost("translate")]
    public async Task<ActionResult<PythonBridge.Contracts.PythonTranslateResponse>> Translate([FromBody] TranslateRequest request, CancellationToken cancellationToken)
    {
        string text = (request.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest("Text is required.");
        }

        request.Text = text;
        PythonBridge.Contracts.PythonTranslateResponse translation = await pythonBackendClient.TranslateAsync(request, cancellationToken);
        return Ok(translation);
    }

    [HttpPost("translate/file")]
    [Consumes("multipart/form-data")]
    [Produces("text/plain")]
    public async Task<IActionResult> TranslateFile([FromForm] TranslateFileRequest request, CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        string translatedText = await pythonBackendClient.TranslateFileAsync(request, cancellationToken);
        return Content(translatedText, "text/plain");
    }

    [HttpPost("translate/file/json")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<PythonBridge.Contracts.PythonTranslateResponse>> TranslateFileJson([FromForm] TranslateFileJsonRequest request, CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        PythonBridge.Contracts.PythonTranslateResponse translation = await pythonBackendClient.TranslateFileJsonAsync(request, cancellationToken);
        return Ok(translation);
    }

    [HttpPost("translate/srt")]
    [Consumes("multipart/form-data")]
    [Produces("text/plain")]
    public async Task<IActionResult> TranslateSrt([FromForm] TranslateSrtRequest request, CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        string translatedSrt = await pythonBackendClient.TranslateSrtAsync(request, cancellationToken);
        return Content(translatedSrt, "text/plain");
    }
}
