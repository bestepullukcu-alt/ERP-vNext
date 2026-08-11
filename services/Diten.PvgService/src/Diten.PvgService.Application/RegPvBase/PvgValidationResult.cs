namespace Diten.PvgService.Application.RegPvBase;

public sealed record PvgValidationResult(IReadOnlyList<PvgValidationFailure> Failures)
{
    public bool IsValid => Failures.Count == 0;

    public static PvgValidationResult Valid { get; } = new([]);
}
