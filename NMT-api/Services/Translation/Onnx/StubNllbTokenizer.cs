using System.Text;

namespace NMT_api.Services.Translation.Onnx;

public sealed class StubNllbTokenizer : ITranslationTokenizer
{
    public int[] Encode(string text, string sourceLanguage, int maxInputTokens)
    {
        _ = sourceLanguage;
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        byte[] bytes = Encoding.UTF8.GetBytes(text.Trim());
        return bytes.Take(maxInputTokens).Select(static b => (int)b).ToArray();
    }

    public int ResolveForcedBosTokenId(string targetLanguage)
    {
        return Math.Abs(targetLanguage.GetHashCode(StringComparison.OrdinalIgnoreCase));
    }

    public string Decode(IReadOnlyList<int> tokenIds)
    {
        if (tokenIds.Count == 0)
        {
            return string.Empty;
        }

        byte[] bytes = tokenIds.Select(static i => (byte)(Math.Abs(i) % 255)).ToArray();
        return Encoding.UTF8.GetString(bytes);
    }
}
