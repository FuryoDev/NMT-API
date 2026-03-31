namespace NMT_api.Services.Translation.Onnx;

public interface ITranslationTokenizer
{
    int[] Encode(string text, string sourceLanguage, int maxInputTokens);
    int ResolveForcedBosTokenId(string targetLanguage);
    string Decode(IReadOnlyList<int> tokenIds);
}
