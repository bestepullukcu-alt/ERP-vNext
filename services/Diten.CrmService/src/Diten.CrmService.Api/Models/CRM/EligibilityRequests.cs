namespace Diten.CrmService.Api.Models.CRM;

// SCMM-11-follow-API (CAND-CAP-0011) eligibility request models. TenantId is NEVER part of any request body (server-
// resolved from the JWT claim). Route ids come from the path. These map 1:1 onto the ready EligibilityPolicy commands
// and the ResolveEligibilityQuery (archive carries only the route id, so it needs no body).

/// <summary>SCMM-11 authored condition — an includes/excludes test of a context dimension (config strings).</summary>
public sealed record EligibilityConditionRequest(
    string Dimension,
    IReadOnlyList<string> Values,
    string? Match = null,
    bool Required = false);

public sealed record CreateEligibilityPolicyRequest(
    string PolicyCode,
    string PolicyName,
    DateTimeOffset EffectiveFrom,
    IReadOnlyList<EligibilityConditionRequest>? Conditions = null,
    string? Description = null,
    string? PolicyVersion = null,
    string? Status = null,
    DateTimeOffset? EffectiveTo = null);

public sealed record UpdateEligibilityPolicyRequest(
    string PolicyName,
    DateTimeOffset EffectiveFrom,
    IReadOnlyList<EligibilityConditionRequest>? Conditions = null,
    string? Description = null,
    string? PolicyVersion = null,
    string? Status = null,
    DateTimeOffset? EffectiveTo = null);

/// <summary>One resolved context dimension for evaluate — an axis key plus the values the subject/context carries on it
/// (audience / product / market / channel / language / period are all expressed the same generic way).</summary>
public sealed record EligibilityContextDimensionRequest(string Dimension, IReadOnlyList<string> Values);

/// <summary>SCMM-11 evaluate request — resolve one context against one policy at an instant. The context is supplied
/// explicitly (no silent default): an empty / malformed context is a 400, never an invented empty context.</summary>
public sealed record EvaluateEligibilityRequest(
    Guid PolicyId,
    IReadOnlyList<EligibilityContextDimensionRequest>? Context = null,
    IReadOnlyList<string>? PinnedSelections = null,
    DateTimeOffset? At = null);
