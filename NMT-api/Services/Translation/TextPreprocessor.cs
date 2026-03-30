using System.Text.RegularExpressions;

namespace NMT_api.Services.Translation;

public static partial class TextPreprocessor
{
    private static readonly IReadOnlyList<(Regex Pattern, string Replacement)> Replacements =
    [
        (RegexOnSEnBatLesCouilles(), "on s'en fiche"),
        (RegexOnSEnFout(), "on s'en fiche"),
        (RegexVasY(), "peu importe"),
        (RegexCeNestPasGrave(), "ce n'est pas grave")
    ];

    public static (string Text, List<string> AppliedRules) Normalize(string text)
    {
        string output = text ?? string.Empty;
        List<string> applied = [];

        foreach ((Regex pattern, string replacement) in Replacements)
        {
            if (!pattern.IsMatch(output))
            {
                continue;
            }

            output = pattern.Replace(output, replacement);
            applied.Add($"{pattern} -> {replacement}");
        }

        return (output, applied);
    }

    [GeneratedRegex("\\bon s'en bat les couilles\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RegexOnSEnBatLesCouilles();

    [GeneratedRegex("\\bon s'en fout\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RegexOnSEnFout();

    [GeneratedRegex("\\bvas[- ]?y\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RegexVasY();

    [GeneratedRegex("\\bc'est pas grave\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RegexCeNestPasGrave();
}
