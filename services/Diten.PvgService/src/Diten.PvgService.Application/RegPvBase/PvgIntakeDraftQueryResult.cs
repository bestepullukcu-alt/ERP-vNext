namespace Diten.PvgService.Application.RegPvBase;

public sealed record PvgIntakeDraftQueryResult(
    PvgApplicationResult Result,
    IReadOnlyList<PvgIntakeDraftSummary> Items);
