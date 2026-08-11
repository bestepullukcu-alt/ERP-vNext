namespace Diten.PvgService.Application.CaseProcessing;

public sealed record PvgCaseProcessingValidationResult(IReadOnlyList<PvgCaseProcessingValidationFailure> Failures)
{
    public bool IsValid => Failures.Count == 0;

    public static PvgCaseProcessingValidationResult Valid { get; } = new([]);
}
