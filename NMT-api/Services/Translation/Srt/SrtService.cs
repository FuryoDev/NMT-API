using System.Text.RegularExpressions;

namespace NMT_api.Services.Translation.Srt;

public sealed partial class SrtService : ISrtService
{
    public SrtDocument Parse(string srtText)
    {
        string normalized = (srtText ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new SrtDocument([]);
        }

        string[] rawBlocks = Regex.Split(normalized, "\n\\s*\n");
        List<SrtBlock> blocks = [];

        foreach (string rawBlock in rawBlocks)
        {
            string[] lines = rawBlock
                .Split('\n')
                .Select(line => line.TrimEnd())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            if (lines.Length < 2)
            {
                continue;
            }

            int index = int.TryParse(lines[0].Trim(), out int parsedIndex)
                ? parsedIndex
                : blocks.Count + 1;

            string timeRange = lines[1].Trim();
            IReadOnlyList<string> textLines = lines.Length > 2
                ? lines[2..]
                : [];

            blocks.Add(new SrtBlock(index, timeRange, textLines));
        }

        return new SrtDocument(blocks);
    }

    public string Build(SrtDocument document)
    {
        List<string> lines = [];

        foreach (SrtBlock block in document.Blocks)
        {
            lines.Add(block.Index.ToString());
            lines.Add(block.TimeRange);
            lines.AddRange(block.Lines);
            lines.Add(string.Empty);
        }

        return string.Join("\n", lines).TrimEnd() + "\n";
    }

    public string JoinBlockText(SrtBlock block)
    {
        return string.Join(" ", block.Lines.Select(line => line.Trim()).Where(line => line.Length > 0)).Trim();
    }

    public IReadOnlyList<string> SplitTextBackToLines(string text, int maxLineLength)
    {
        string normalized = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        List<string> lines = [];
        List<string> currentWords = [];
        int currentLength = 0;

        foreach (string word in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            int additionalLength = word.Length + (currentWords.Count > 0 ? 1 : 0);
            if (currentWords.Count > 0 && currentLength + additionalLength > maxLineLength)
            {
                lines.Add(string.Join(" ", currentWords));
                currentWords = [word];
                currentLength = word.Length;
            }
            else
            {
                currentWords.Add(word);
                currentLength += additionalLength;
            }
        }

        if (currentWords.Count > 0)
        {
            lines.Add(string.Join(" ", currentWords));
        }

        return lines;
    }
}
