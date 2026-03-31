namespace NMT_api.Services.Translation.Configuration;

public sealed class NllbOnnxOptions
{
    public const string SectionName = "Translation:NllbOnnx";

    public string ModelName { get; set; } = "facebook/nllb-200-distilled-600M";
    public string Device { get; set; } = "cpu";
    public string ModelPath { get; set; } = "models/nllb/model.onnx";
    public int MaxInputTokens { get; set; } = 512;
    public bool EnableOnnxRuntime { get; set; } = false;
    public int DefaultMaxNewTokens { get; set; } = 256;
    public int DefaultNumBeams { get; set; } = 4;
    public int NoRepeatNgramSize { get; set; } = 3;
    public float RepetitionPenalty { get; set; } = 1.15f;
}
