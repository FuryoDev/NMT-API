namespace NMT_api.Services.Translation.Onnx;

public class NllbOnnxOptions
{
    public string ModelPath { get; set; } = "Models/Nllb/nllb_test.onnx";
    public string TokenizerPath { get; set; } = "Models/Nllb/tokenizer.json";
    public bool ModelRequired { get; set; } = true;
    public int MaxNewTokens {get; set;} = 50;
    public long EosTokenId { get; set; } = 2;
    public long TargetLanguageTokenId { get; set; } = 256047;
}
