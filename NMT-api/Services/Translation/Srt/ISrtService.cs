namespace NMT_api.Services.Translation.Srt;

public interface ISrtService
{
    SrtDocument Parse(string srtText);
    string Build(SrtDocument document);
    string JoinBlockText(SrtBlock block);
    IReadOnlyList<string> SplitTextBackToLines(string text, int maxLineLength);
}
