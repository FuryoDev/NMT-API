namespace NMT_api.Services.Translation.Language;

public sealed class TranslationLanguageOptions
{
    public Dictionary<string, string> SupportedLanguages { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ar"] = "arb_Arab",
        ["cs"] = "ces_Latn",
        ["da"] = "dan_Latn",
        ["de"] = "deu_Latn",
        ["el"] = "ell_Grek",
        ["en"] = "eng_Latn",
        ["es"] = "spa_Latn",
        ["fa"] = "pes_Arab",
        ["fr"] = "fra_Latn",
        ["he"] = "heb_Hebr",
        ["hr"] = "hrv_Latn",
        ["hu"] = "hun_Latn",
        ["it"] = "ita_Latn",
        ["ja"] = "jpn_Jpan",
        ["ko"] = "kor_Hang",
        ["nl"] = "nld_Latn",
        ["no"] = "nob_Latn",
        ["nb"] = "nob_Latn",
        ["nn"] = "nno_Latn",
        ["pl"] = "pol_Latn",
        ["pt"] = "por_Latn",
        ["ro"] = "ron_Latn",
        ["ru"] = "rus_Cyrl",
        ["sk"] = "slk_Latn",
        ["sl"] = "slv_Latn",
        ["tr"] = "tur_Latn",
        ["uk"] = "ukr_Cyrl",
        ["zh"] = "zho_Hans",
        ["zh-Hans"] = "zho_Hans",
        ["zh-Hant"] = "zho_Hant"
    };
}
