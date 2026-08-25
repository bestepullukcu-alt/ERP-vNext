namespace Diten.PvgService.Application.CaseProcessing;

public sealed record PvgCaseProcessingValidationFailure(string? Field, string ReasonCode);
