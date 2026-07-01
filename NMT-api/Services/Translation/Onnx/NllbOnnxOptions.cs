namespace NMT_api.Services.Translation.Onnx;

public class NllbOnnxOptions
{
    public string ModelPath { get; set; } = "Models/Nllb/nllb-200-3.3b.onnx";
    public string TokenizerPath { get; set; } = "Models/Nllb/tokenizer.json";
    public bool ModelRequired { get; set; } = true;
    public int MaxNewTokens { get; set; } = 512;
    public long EosTokenId { get; set; } = 2;
    public DecodingMode DecodingMode { get; set; } = DecodingMode.Greedy;
    public int NumBeams { get; set; } = 1;
    public double LengthPenalty { get; set; } = 1.0;
    public int NoRepeatNgramSize { get; set; }
    public double RepetitionPenalty { get; set; } = 1.0;
}
