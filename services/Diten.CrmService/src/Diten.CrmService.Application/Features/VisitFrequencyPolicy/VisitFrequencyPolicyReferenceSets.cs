namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy;

/// <summary>
/// MOD-0165 FU03 — the MOD-0048 / PSS-012 reference SET CODES this feature would consume once published, exposed on
/// the contract for readiness reporting only. FU03 validates the frequency vocabulary (target/frequency/period/source/
/// status) <b>in-domain</b> as structural constants (see the <c>Frequency*</c> classes in the Domain layer), the same
/// way MOD-0150 validates ISO weekdays in-domain — so the runtime does NOT require these sets to be published. CRM
/// never seeds or hardcodes reference VALUES; only these set codes and the F1 expected counts live in code, and they
/// are never used as a validation fallback. MOD-0048 publish is out of FU03 scope.
/// </summary>
public static class VisitFrequencyPolicyReferenceSets
{
    public const string FrequencyTargetType = "visit-frequency-target-type";
    public const string FrequencyType = "visit-frequency-type";
    public const string FrequencyPeriodType = "visit-frequency-period-type";
    public const string FrequencySource = "visit-frequency-source";
    public const string FrequencyStatus = "visit-frequency-status";

    /// <summary>The reference sets FU03 would consume, in readiness-report order, with the F1 template expected value
    /// counts. Marked non-blocking on the contract: the runtime uses in-domain vocabulary, so an unpublished set does
    /// not stop authoring — it is surfaced only so operators can plan the eventual MOD-0048 alignment.</summary>
    public static readonly IReadOnlyList<VisitFrequencyReferenceSetDescriptor> Optional = new[]
    {
        new VisitFrequencyReferenceSetDescriptor(FrequencyTargetType, 8),
        new VisitFrequencyReferenceSetDescriptor(FrequencyType, 5),
        new VisitFrequencyReferenceSetDescriptor(FrequencyPeriodType, 7),
        new VisitFrequencyReferenceSetDescriptor(FrequencySource, 7),
        new VisitFrequencyReferenceSetDescriptor(FrequencyStatus, 4),
    };
}

/// <summary>Readiness descriptor: expected value count of the F1 template for a would-be MOD-0048 set.</summary>
public sealed record VisitFrequencyReferenceSetDescriptor(string SetCode, int ExpectedValueCount);
