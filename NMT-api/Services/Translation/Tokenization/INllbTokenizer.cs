namespace NMT_api.Services.Translation.Tokenization;

public interface INllbTokenizer
{
    long[] Encode(string text, string sourceLanguage);
    string Decode(IEnumerable<long> tokenIds);
    long GetTokenId(string token);
    long EosTokenId { get; }
}