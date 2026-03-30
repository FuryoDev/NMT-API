using System.Text;
using System.Text.RegularExpressions;
using NMT_api.Services.Translation;

namespace NMT_api.Services.Srt;

public partial class SrtTranslationService(INmtTranslationService translator) : ISrtTranslationService
{
    public string TranslateSrt(
        string srtText,
        string sourceLanguage,
        string targetLanguage,
        int maxNewTokens = 256,
        int numBeams = 4)
    {
        List<SrtBlock> blocks = ParseSrt(srtText);
        if (blocks.Count == 0)
        {
            return string.Empty;
        }

        foreach (SrtBlock block in blocks)
        {
            string sourceText = JoinBlockText(block);
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                block.Lines = [];
                continue;
            }

            TranslationResult translation = translator.Translate(
                sourceText,
                sourceLanguage,
                targetLanguage,
                maxNewTokens,
                numBeams);

            block.Lines = SplitTextBackToLines(translation.TranslatedText);
        }

        return BlocksToSrt(blocks);
    }

    private static List<SrtBlock> ParseSrt(string srtText)
    {
        string normalized = (srtText ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        string[] rawBlocks = Regex.Split(normalized, "\\n\\s*\\n");
        List<SrtBlock> output = [];

        foreach (string raw in rawBlocks)
        {
            List<string> lines = raw
                .Split('\n')
                .Select(line => line.TrimEnd())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            if (lines.Count < 2)
            {
                continue;
            }

            int index = int.TryParse(lines[0], out int parsedIndex) ? parsedIndex : output.Count + 1;
            string timeRange = lines[1];
            List<string> textLines = lines.Count > 2 ? lines.Skip(2).ToList() : [];

            output.Add(new SrtBlock
            {
                Index = index,
                TimeRange = timeRange,
                Lines = textLines
            });
        }

        return output;
    }

    private static string BlocksToSrt(IReadOnlyCollection<SrtBlock> blocks)
    {
        StringBuilder sb = new();

        foreach (SrtBlock block in blocks)
        {
            _ = sb.AppendLine(block.Index.ToString());
            _ = sb.AppendLine(block.TimeRange);

            foreach (string line in block.Lines)
            {
                _ = sb.AppendLine(line);
            }

            _ = sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    private static string JoinBlockText(SrtBlock block)
    {
        return string.Join(' ', block.Lines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim())).Trim();
    }

    private static List<string> SplitTextBackToLines(string text, int maxLineLength = 42)
    {
        string normalized = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        string[] words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<string> lines = [];
        List<string> buffer = [];
        int currentLength = 0;

        foreach (string word in words)
        {
            int extraLength = word.Length + (buffer.Count > 0 ? 1 : 0);

            if (buffer.Count > 0 && currentLength + extraLength > maxLineLength)
            {
                lines.Add(string.Join(' ', buffer));
                buffer = [word];
                currentLength = word.Length;
                continue;
            }

            buffer.Add(word);
            currentLength += extraLength;
        }

        if (buffer.Count > 0)
        {
            lines.Add(string.Join(' ', buffer));
        }

        return lines;
    }
}
