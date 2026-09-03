namespace Diten.PpmService.Domain.Entities;

public static class InitiativeVocabularies
{
    public static IReadOnlyList<string> CancellationReasons { get; } =
    ["strategic-realignment", "funding-withdrawn", "business-case-rejected", "duplicate-initiative", "regulatory-block", "capacity-unavailable", "no-longer-viable", "superseded"];
    public static IReadOnlyList<string> HoldReasons { get; } =
    ["funding-paused", "capacity-constraint", "dependency-blocked", "governance-review", "strategy-review", "external-constraint"];
    public static IReadOnlyList<string> CompletionOutcomes { get; } =
    ["delivered-as-planned", "delivered-with-variance", "partially-delivered", "transferred-to-operations"];
    public static IReadOnlyList<string> ClosureReasons { get; } =
    ["scope-completed", "planned-end-reached", "governance-directed-close", "early-completion"];
    public static IReadOnlyList<string> BenefitDispositions { get; } =
    ["tracking-required", "tracking-in-progress", "handed-off-to-outcome-owner", "no-benefit-commitment"];

    public static string RequireCancellationReason(string value) => Require(value, CancellationReasons, "cancellation reason");
    public static string RequireHoldReason(string value) => Require(value, HoldReasons, "hold reason");
    public static string RequireCompletionOutcome(string value) => Require(value, CompletionOutcomes, "completion outcome");
    public static string RequireClosureReason(string value) => Require(value, ClosureReasons, "closure reason");
    public static string RequireBenefitDisposition(string value) => Require(value, BenefitDispositions, "benefit disposition");

    private static string Require(string value, IReadOnlyList<string> allowed, string name)
    {
        if (value is null || !allowed.Contains(value, StringComparer.Ordinal))
            throw new ArgumentException($"Unknown Initiative {name} code.", name);
        return value;
    }
}
