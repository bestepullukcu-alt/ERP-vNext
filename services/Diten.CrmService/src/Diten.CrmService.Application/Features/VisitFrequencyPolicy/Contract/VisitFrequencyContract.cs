using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy.Contract;

public sealed record GetVisitFrequencyContractQuery : IRequest<Response<VisitFrequencyContractDto>>;

/// <summary>MOD-0165 FU03 contract surface (feature flags + supported vocabulary + permissions + limitations).</summary>
public sealed record VisitFrequencyContractDto(
    string ModuleId,
    string ModuleName,
    string Service,
    string RuntimeScope,
    Guid TenantId,
    bool IsReady,
    VisitFrequencyFeatureFlags Features,
    VisitFrequencyVocabulary Vocabulary,
    IReadOnlyList<string> OptionalReferenceSets,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Limitations);

/// <summary>
/// The FU03 capability flags. ONLY the frequency-policy flags are present. The planning / detailing / recommendation /
/// consent-engine / workflow flags are deliberately ABSENT (never emitted as false either) — this task opens none of
/// those capabilities, and advertising them (even as false) would misrepresent the boundary.
/// </summary>
public sealed record VisitFrequencyFeatureFlags(
    bool SupportsVisitFrequencyPolicy,
    bool SupportsCallCyclePolicy,
    bool SupportsFrequencyPolicyPriority,
    bool SupportsFrequencyPolicyEffectiveWindow,
    bool SupportsFrequencyPolicyProvider)
{
    public static VisitFrequencyFeatureFlags Current => new(
        SupportsVisitFrequencyPolicy: true,
        SupportsCallCyclePolicy: true,
        SupportsFrequencyPolicyPriority: true,
        SupportsFrequencyPolicyEffectiveWindow: true,
        SupportsFrequencyPolicyProvider: true);
}

/// <summary>The in-domain vocabulary the runtime validates against (surfaced so authoring UIs need no hardcoded list).</summary>
public sealed record VisitFrequencyVocabulary(
    IReadOnlyList<string> TargetTypes,
    IReadOnlyList<string> FrequencyTypes,
    IReadOnlyList<string> PeriodTypes,
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> Statuses)
{
    public static VisitFrequencyVocabulary Current => new(
        FrequencyTargetType.All,
        FrequencyType.All,
        FrequencyPeriodType.All,
        FrequencySource.All,
        FrequencyPolicyStatus.All);
}

public sealed class GetVisitFrequencyContractHandler
    : IRequestHandler<GetVisitFrequencyContractQuery, Response<VisitFrequencyContractDto>>
{
    public const string ModuleId = "MOD-0165";
    public const string ModuleName = "Visit Frequency / Call-Cycle Policy";
    public const string Service = "Diten.CrmService";
    public const string RuntimeScope =
        "FU01-visit-frequency-policy-ownership; FU02-campaign-targeting-boundary; " +
        "FU03-visit-frequency-call-cycle-policy-implementation (authoring + read/resolve provider)";

    private static readonly IReadOnlyList<string> CurrentLimitations = new[]
    {
        "frequency policy answers ONLY 'how often should this target be visited?' — never due/overdue, last-visit, route, order, content or consent",
        "resolve provider is GET/read-only: it performs no writes and returns no route/visit/due/last-visit/consent field",
        "no policy match returns FrequencyStatus=unknown — a default frequency is never invented",
        "a same-band tie is resolved deterministically by stable PolicyId and flagged FrequencyStatus=conflict (still 200 + diagnostics)",
        "frequency vocabulary is validated in-domain (structural); MOD-0048 publish is out of FU03 scope",
        "segment/campaign/brand/product are provenance + context only — segment membership, campaign runtime and brand/product master are never opened here",
        "there is no DELETE; closing a policy is a soft archive (ArchivedAt/By stamped), still readable as history",
        "PolicyCode is stable; renaming is done through PolicyName",
        "MOD-0151 FU09A integration is a read-only follow-up (MOD-0151-FU09B); this task does not modify MOD-0151",
        "UI is a follow-up; FU03 ships the API + resolve provider + tests"
    };

    private readonly ITenantContext _tenant;

    public GetVisitFrequencyContractHandler(ITenantContext tenant)
    {
        _tenant = tenant;
    }

    public Task<Response<VisitFrequencyContractDto>> Handle(GetVisitFrequencyContractQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Task.FromResult(Response<VisitFrequencyContractDto>.Fail("Tenant context is required.", 400));
        }

        var dto = new VisitFrequencyContractDto(
            ModuleId,
            ModuleName,
            Service,
            RuntimeScope,
            tenantId,
            IsReady: true, // vocabulary is in-domain, so authoring is ready without a MOD-0048 publish
            VisitFrequencyFeatureFlags.Current,
            VisitFrequencyVocabulary.Current,
            VisitFrequencyPolicyReferenceSets.Optional.Select(s => s.SetCode).ToList(),
            VisitFrequencyPolicyPermissions.All,
            CurrentLimitations);

        return Task.FromResult(Response<VisitFrequencyContractDto>.Success(dto));
    }
}
