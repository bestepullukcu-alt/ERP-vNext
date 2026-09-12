using System.Text.Json.Serialization;

namespace Diten.CrmService.Application.Features.ContentComposition.Eligibility;

// SCMM-11 (CAND-CAP-0011) — the application-layer vocabulary the eligibility resolver (ResolveEligibilityQuery /
// Handler) and its in-process gate (IEligibilityEvaluationPort) both speak. Mirrors the MOD-0029 controlled-document
// effectiveness contract: a DISJOINT outcome + fail-closed semantics. This slice evaluates a CONTEXT against a policy;
// it computes no segment membership (MOD-0167) and advances no content (execution).

/// <summary>
/// SCMM-11 — the disjoint outcome of evaluating a context against an eligibility policy. The three states carry
/// different meanings and MUST stay distinct (DEC-SCMM-03 D3a / RM4):
/// <list type="bullet">
/// <item><see cref="Eligible"/> — every condition is satisfied.</item>
/// <item><see cref="Blocked"/> — a condition is definitively violated (the context has/omits a value the policy forbids/requires).</item>
/// <item><see cref="Unresolved"/> — a REQUIRED context dimension is missing, or no effective policy applies: the gate
/// cannot decide (fail-closed). Never used for an infrastructure failure — a repository read that throws propagates as a
/// thrown exception, never as <see cref="Unresolved"/>.</item>
/// </list>
/// A gate treats anything other than <see cref="Eligible"/> as a refusal (RED). Carries the per-enum
/// <c>JsonStringEnumConverter</c> so it never serializes as a numeric value on any future HTTP boundary.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EligibilityState
{
    Eligible,
    Blocked,
    Unresolved
}

/// <summary>SCMM-11 — one resolved context dimension: an axis key plus the values the subject/context carries on it
/// (audience axes come from <c>AudienceProfile.Dimensions</c>; product/market/channel/language/period are single-value
/// axes expressed the same way). Config strings only.</summary>
public sealed record EligibilityContextDimension(string Dimension, IReadOnlyList<string> Values);

/// <summary>SCMM-11 — the evaluation context: the resolved dimensions of the subject being checked. This is DATA the
/// caller supplies; the resolver never invents a value for a missing dimension (fail-closed).</summary>
public sealed record EligibilityContext(IReadOnlyList<EligibilityContextDimension> Dimensions);

/// <summary>SCMM-11 — the per-condition outcome, surfaced on the result so a non-Eligible decision is never silent.</summary>
public sealed record EligibilityConditionOutcome(
    string Dimension,
    string Match,
    EligibilityState State,
    string? Reason);

/// <summary>
/// SCMM-11 — the resolved eligibility of one context against one policy. <see cref="BlockingLevel"/> is <c>context</c>
/// (a required dimension was missing) or <c>policy</c> (a condition was violated / no effective policy) and null when
/// <see cref="State"/> is <see cref="EligibilityState.Eligible"/>. <see cref="Reason"/> is the first blocking/unresolved
/// reason code. <see cref="Conditions"/> holds every condition's outcome; <see cref="PolicyVersion"/> is echoed for the
/// evaluation log (RM4).
/// </summary>
public sealed record EligibilityResult(
    EligibilityState State,
    string? BlockingLevel,
    string? Reason,
    Guid PolicyId,
    string PolicyVersion,
    IReadOnlyList<EligibilityConditionOutcome> Conditions,
    DateTimeOffset EvaluatedAtUtc);

/// <summary>
/// SCMM-11 (RM4) — the evaluation log entry: input context + pinned selections + result + reasons + policy version.
/// Written on every evaluation so an eligibility decision is always accountable. Recording is a side effect (fail-soft):
/// it never changes or blocks the decision the resolver already computed.
/// </summary>
public sealed record EligibilityEvaluationLogEntry(
    Guid TenantId,
    Guid PolicyId,
    string PolicyVersion,
    EligibilityContext Context,
    IReadOnlyList<string> PinnedSelections,
    EligibilityState State,
    string? BlockingLevel,
    IReadOnlyList<string> Reasons,
    DateTimeOffset EvaluatedAtUtc,
    string? Actor);
