namespace Diten.PvgService.Application.CaseProcessing;

public sealed record PvgCaseProcessingQueryResult(
    PvgCaseProcessingResult Result,
    IReadOnlyList<CaseProcessingMetadataSummary> Items);
