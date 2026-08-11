namespace Diten.PvgService.Application.RegPvBase;

public sealed record PvgApplicationResult(
    PvgApplicationOutcome Outcome,
    PvgApplicationSuccessMetadata? Metadata,
    string? ReasonCode,
    IReadOnlyList<PvgValidationFailure> ValidationFailures)
{
    public bool IsSuccess => Outcome == PvgApplicationOutcome.Succeeded;

    public static PvgApplicationResult Succeeded(PvgApplicationSuccessMetadata metadata) =>
        new(PvgApplicationOutcome.Succeeded, metadata, null, []);

    public static PvgApplicationResult Invalid(IReadOnlyList<PvgValidationFailure> validationFailures) =>
        new(PvgApplicationOutcome.Invalid, null, PvgValidationReasonCodes.FieldValueInvalid, validationFailures);

    public static PvgApplicationResult Blocked(string reasonCode) =>
        new(PvgApplicationOutcome.Blocked, null, reasonCode, []);
}
