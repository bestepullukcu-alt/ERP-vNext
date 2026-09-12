using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.Eligibility;

/// <summary>SCMM-11 (CAND-CAP-0011, RM4) — one authored condition: an includes/excludes test of a context dimension.
/// Values are config / reference strings (sector-neutral).</summary>
public sealed record EligibilityConditionInput(
    string Dimension,
    IReadOnlyList<string> Values,
    string? Match = null,
    bool Required = false);

/// <summary>SCMM-11 (CAND-CAP-0011) eligibility policy write surface. <c>TenantId</c> is server-resolved. No delete —
/// closing a policy is <see cref="ArchiveEligibilityPolicyCommand"/>. A published version freezes its conditions
/// (change ⇒ new version). <c>PolicyVersion</c> is the business version, not the concurrency token.</summary>
public sealed record CreateEligibilityPolicyCommand(
    string PolicyCode,
    string PolicyName,
    DateTimeOffset EffectiveFrom,
    IReadOnlyList<EligibilityConditionInput> Conditions,
    string? Description = null,
    string? PolicyVersion = null,
    string? Status = null,
    DateTimeOffset? EffectiveTo = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the mutable fields. <c>PolicyCode</c> is immutable. An archived policy cannot be updated; a
/// published policy freezes its <c>Conditions</c> (change ⇒ new version).</summary>
public sealed record UpdateEligibilityPolicyCommand(
    Guid EligibilityPolicyId,
    string PolicyName,
    DateTimeOffset EffectiveFrom,
    IReadOnlyList<EligibilityConditionInput> Conditions,
    string? Description = null,
    string? PolicyVersion = null,
    string? Status = null,
    DateTimeOffset? EffectiveTo = null) : IRequest<Response<bool>>;

public sealed record ArchiveEligibilityPolicyCommand(Guid EligibilityPolicyId) : IRequest<Response<bool>>;
