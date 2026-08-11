namespace Diten.PvgService.Application.CaseProcessing;

public enum PvgCaseProcessingOutcome
{
    Accepted = 0,
    Invalid = 1,
    Blocked = 2,
    NotFound = 3
}

public sealed record PvgCaseProcessingResult(
    PvgCaseProcessingOutcome Outcome,
    IReadOnlyList<string> ReasonCodes,
    PvgCaseProcessingSuccessMetadata? Metadata = null)
{
    public bool Succeeded => Outcome == PvgCaseProcessingOutcome.Accepted;

    public static PvgCaseProcessingResult Accepted(PvgCaseProcessingSuccessMetadata metadata) =>
        new(PvgCaseProcessingOutcome.Accepted, [], metadata);

    public static PvgCaseProcessingResult Invalid(params string[] reasonCodes) =>
        new(PvgCaseProcessingOutcome.Invalid, SafeReasons(reasonCodes));

    public static PvgCaseProcessingResult Blocked(params string[] reasonCodes) =>
        new(PvgCaseProcessingOutcome.Blocked, SafeReasons(reasonCodes));

    public static PvgCaseProcessingResult NotFound() =>
        new(PvgCaseProcessingOutcome.NotFound, ["PVG_CASE_PROCESSING_NOT_FOUND"]);

    private static IReadOnlyList<string> SafeReasons(string[] reasonCodes) =>
        reasonCodes
            .Where(reasonCode => !string.IsNullOrWhiteSpace(reasonCode))
            .Select(reasonCode => reasonCode.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}

public sealed record PvgCaseProcessingSuccessMetadata(
    string Operation,
    string RequiredPermission,
    string ActorKind,
    bool HasCorrelation);
