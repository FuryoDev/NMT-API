using Microsoft.AspNetCore.Mvc;

namespace NMT_api.Contracts.Requests;

public class TranslateSrtRequest
{
    [FromForm(Name = "file")]
    public IFormFile? File { get; set; }

    [FromForm(Name = "sourceLanguage")]
    public string SourceLanguage { get; set; } = "fr";

    [FromForm(Name = "targetLanguage")]
    public string TargetLanguage { get; set; } = "en";

    [FromForm(Name = "maxNewTokens")]
    public int MaxNewTokens { get; set; } = 512;

    [FromForm(Name = "numBeams")]
    public int NumBeams { get; set; } = 5;
}
