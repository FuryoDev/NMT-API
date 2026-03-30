namespace NMT_api.Services.Srt;

public interface ISrtTranslationService
{
    string TranslateSrt(
        string srtText,
        string sourceLanguage,
        string targetLanguage,
        int maxNewTokens = 256,
        int numBeams = 4);
}
