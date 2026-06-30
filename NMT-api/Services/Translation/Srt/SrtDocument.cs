namespace NMT_api.Services.Translation.Srt;

public sealed record SrtDocument(IReadOnlyList<SrtBlock> Blocks);
