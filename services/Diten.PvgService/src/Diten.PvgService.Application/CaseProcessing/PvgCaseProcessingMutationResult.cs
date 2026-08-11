namespace Diten.PvgService.Application.CaseProcessing;

public sealed record PvgCaseProcessingMutationResult(
    PvgCaseProcessingResult Result,
    string? CaseProcessingId);
