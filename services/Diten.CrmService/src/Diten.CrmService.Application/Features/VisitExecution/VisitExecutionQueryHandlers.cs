using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Artifacts;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using ReadAll = Diten.CrmService.Application.Features.PlannedVisit.PlannedVisitPermissions;

namespace Diten.CrmService.Application.Features.VisitExecution;

/// <summary>WP-VW-W3 (BE-a) — <c>GET contract</c>: the effective policy (stored or the defaults; the read writes
/// nothing), the vocabularies, the limits, the error codes and the permission keys.</summary>
public sealed class GetVisitExecutionContractHandler : IRequestHandler<GetVisitExecutionContractQuery, Response<VisitExecutionContractDto>>
{
    public const string ModuleId = "MOD-0155-VX";

    private readonly ITenantContext _tenant;
    private readonly IVisitEvidencePolicyRepository _policies;
    private readonly ICallerScope? _caller;
    private readonly IVisitEvidencePolicyResolver _resolver;
    private readonly TimeProvider _clock;

    /// <summary>WP-VW-W3 (W3-BE-c) — <c>policy</c> is the CALLER's effective rules (tenant ⊕ an approved exception active
    /// today) through the one resolver the start uses; a caller without a resource id reads the tenant's.</summary>
    public GetVisitExecutionContractHandler(ITenantContext tenant, IVisitEvidencePolicyRepository policies,
        ICallerScope? caller = null, IVisitEvidencePolicyResolver? resolver = null, TimeProvider? clock = null)
    {
        _tenant = tenant;
        _policies = policies;
        _caller = caller;
        _resolver = resolver ?? new VisitEvidencePolicyResolver(policies);
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<VisitExecutionContractDto>> Handle(GetVisitExecutionContractQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitExecutionContractDto>.Fail("Tenant context is required.", 400);
        }

        var effective = await _resolver.ResolveAsync(tenantId, _caller?.CallerResourceId, _clock.GetUtcNow(), cancellationToken);
        return Response<VisitExecutionContractDto>.Success(new VisitExecutionContractDto(
            ModuleId,
            VisitExecutionRules.ToDto(effective.Policy, effective.IsDefault),
            VisitExecutionKinds.All,
            VisitInterestValues.All,
            VisitMarkFlags.All,
            VisitExecutionFlags.All,
            VisitLocationErrors.All,
            VisitSignatureContexts.All,
            VisitExecutionLimits.ObjectionCategorySet,
            VisitExecutionLimits.MaxNoteLength,
            VisitExecutionLimits.MaxPersonalNoteLength,
            VisitExecutionLimits.MaxCoVisitors,
            VisitExecutionLimits.MaxEvidenceBytes,
            VisitExecutionRules.PhotoMediaTypes,
            VisitExecutionErrorCodes.All,
            VisitExecutionPermissions.All));
    }
}

/// <summary>WP-VW-W3 (BE-a) — <c>GET by-plan/{plannedVisitId}</c>: the execution (or <c>data: null</c> when the visit
/// is not started). Own visit, or any with read-all; another rep's visit is 404. Personal notes go to the author only.</summary>
public sealed class GetVisitExecutionByPlanHandler : IRequestHandler<GetVisitExecutionByPlanQuery, Response<VisitExecutionDto?>>
{
    private readonly ITenantContext _tenant;
    private readonly ICallerScope _caller;
    private readonly IPlannedVisitRepository _plans;
    private readonly IVisitExecutionRepository _executions;
    private readonly IUserDisplayNameResolver _users;

    public GetVisitExecutionByPlanHandler(
        ITenantContext tenant, ICallerScope caller, IPlannedVisitRepository plans, IVisitExecutionRepository executions,
        IUserDisplayNameResolver users)
    {
        _tenant = tenant;
        _caller = caller;
        _plans = plans;
        _executions = executions;
        _users = users;
    }

    public async Task<Response<VisitExecutionDto?>> Handle(GetVisitExecutionByPlanQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitExecutionDto?>.Fail("Tenant context is required.", 400);
        }

        var plan = await _plans.GetByIdAsync(tenantId, request.PlannedVisitId, cancellationToken);
        if (plan is null || !VisitExecutionAccess.MayRead(_caller, plan))
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto?>(VisitExecutionAccess.PlanNotFound());
        }

        var execution = await _executions.GetByPlannedVisitIdAsync(tenantId, plan.Id, cancellationToken);
        if (execution is null)
        {
            return Response<VisitExecutionDto?>.Success(null);
        }

        var ids = execution.CoVisitorUserIds.Select(v => Guid.TryParse(v, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).ToList();
        var names = ids.Count > 0 ? await _users.ResolveAsync(ids, cancellationToken) : null;
        return Response<VisitExecutionDto?>.Success(
            VisitExecutionRules.ToDto(execution, plan.PlannedDate, _caller.CallerResourceId, names));
    }
}

/// <summary>
/// WP-VW-W3 (BE-a, decision 7) — <c>GET by-plan/{plannedVisitId}/brief</c>: "for this doctor" from the existing report
/// data — the last SUBMITTED report of an earlier visit to the same target that the caller may see (their own, or any
/// with read-all): its date, outcome, outcome code, feedback, follow-up and samples. None ⇒ the empty brief.
/// </summary>
public sealed class GetVisitBriefHandler : IRequestHandler<GetVisitBriefQuery, Response<VisitBriefDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICallerScope _caller;
    private readonly IPlannedVisitRepository _plans;
    private readonly IVisitReportRepository _reports;

    public GetVisitBriefHandler(ITenantContext tenant, ICallerScope caller, IPlannedVisitRepository plans, IVisitReportRepository reports)
    {
        _tenant = tenant;
        _caller = caller;
        _plans = plans;
        _reports = reports;
    }

    public async Task<Response<VisitBriefDto>> Handle(GetVisitBriefQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitBriefDto>.Fail("Tenant context is required.", 400);
        }

        var plan = await _plans.GetByIdAsync(tenantId, request.PlannedVisitId, cancellationToken);
        if (plan is null || !VisitExecutionAccess.MayRead(_caller, plan))
        {
            return VisitExecutionAccess.Fail<VisitBriefDto>(VisitExecutionAccess.PlanNotFound());
        }

        var earlier = (await _plans.ListAsync(tenantId, cancellationToken))
            .Where(v => v.Id != plan.Id && v.TargetId == plan.TargetId && v.PlannedDate <= plan.PlannedDate
                        && VisitExecutionAccess.MayRead(_caller, v))
            .Select(v => v.Id)
            .ToList();
        var last = earlier.Count == 0
            ? null
            : (await _reports.ListByPlannedVisitIdsAsync(tenantId, earlier, cancellationToken))
                .Where(r => r.IsFinalised())
                .OrderByDescending(r => r.ExecutedAt)
                .FirstOrDefault();

        return Response<VisitBriefDto>.Success(last is null
            ? new VisitBriefDto(plan.Id, null, null, null, null, null, null, Array.Empty<VisitBriefSampleDto>())
            : new VisitBriefDto(
                plan.Id,
                last.ExecutedAt.UtcDateTime.ToString("yyyy-MM-dd"),
                last.ExecutionOutcome,
                last.Feedback?.OutcomeCode,
                last.Feedback?.DoctorFeedback,
                last.Feedback?.FollowUpRequired,
                last.Feedback?.FollowUpNotes,
                last.Samples.Select(s => new VisitBriefSampleDto(s.ItemType, s.ItemId, s.Quantity)).ToList()));
    }
}

/// <summary>
/// WP-VW-W3 (BE-a) — <c>GET {id}/photo</c> · <c>GET {id}/signature</c>: the evidence bytes streamed through the CRM.
/// Two gates: the CRM ownership (own visit, or read-all) AND the Platform's scoped key on the forwarded user token (a
/// rep reads only the evidence they stored; a manager needs <c>visit-evidence.read-all</c>). Either refusal is 404.
/// The content id never appears in a response.
/// </summary>
public sealed class GetVisitEvidenceContentHandler : IRequestHandler<GetVisitEvidenceContentQuery, Response<ContentArtifactReadResult>>
{
    private readonly ITenantContext _tenant;
    private readonly ICallerScope _caller;
    private readonly IPlannedVisitRepository _plans;
    private readonly IVisitExecutionRepository _executions;
    private readonly IVisitEvidenceStore _store;

    public GetVisitEvidenceContentHandler(
        ITenantContext tenant, ICallerScope caller, IPlannedVisitRepository plans, IVisitExecutionRepository executions,
        IVisitEvidenceStore store)
    {
        _tenant = tenant;
        _caller = caller;
        _plans = plans;
        _executions = executions;
        _store = store;
    }

    public async Task<Response<ContentArtifactReadResult>> Handle(GetVisitEvidenceContentQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ContentArtifactReadResult>.Fail("Tenant context is required.", 400);
        }

        var execution = await _executions.GetByIdAsync(tenantId, request.ExecutionId, cancellationToken);
        var plan = execution is null ? null : await _plans.GetByIdAsync(tenantId, execution.PlannedVisitId, cancellationToken);
        if (execution is null || plan is null || !_caller.MayAccess(ReadAll.ReadAll, plan.Resource.ResourceId))
        {
            return NotFound();
        }

        var contentId = request.Kind == VisitEvidenceKinds.Signature ? execution.Signature?.ContentId : execution.Photo?.ContentId;
        if (contentId is not { } id || id == Guid.Empty)
        {
            return NotFound();
        }

        ContentArtifactReadResult? read;
        try
        {
            read = await _store.OpenReadAsync(id, cancellationToken);
        }
        catch (ContentArtifactStoreException)
        {
            return Response<ContentArtifactReadResult>.Fail(
                new[] { "The evidence store is unavailable.", VisitExecutionErrorCodes.EvidenceStoreUnavailable }, 503);
        }

        return read is null
            ? NotFound()
            : Response<ContentArtifactReadResult>.Success(read with { FileName = $"visit-{request.Kind}{ExtensionOf(read.MediaType)}" });
    }

    private static string ExtensionOf(string mediaType) => mediaType == "image/png" ? ".png" : ".jpg";

    private static Response<ContentArtifactReadResult> NotFound()
        => Response<ContentArtifactReadResult>.Fail(
            new[] { "The evidence does not exist.", VisitExecutionErrorCodes.NotFound }, 404);
}

/// <summary>WP-VW-W3 (BE-a, K-W2) — <c>GET api/crm/visit-evidence-policy</c>: the stored policy or the defaults
/// (<c>isDefault: true</c>). A read never writes the document.</summary>
public sealed class GetVisitEvidencePolicyHandler : IRequestHandler<GetVisitEvidencePolicyQuery, Response<VisitEvidencePolicyDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IVisitEvidencePolicyRepository _policies;
    private readonly IVisitEvidenceRepRoster? _reps;

    public GetVisitEvidencePolicyHandler(ITenantContext tenant, IVisitEvidencePolicyRepository policies, IVisitEvidenceRepRoster? reps = null)
    {
        _tenant = tenant;
        _policies = policies;
        _reps = reps;
    }

    public async Task<Response<VisitEvidencePolicyDto>> Handle(GetVisitEvidencePolicyQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitEvidencePolicyDto>.Fail("Tenant context is required.", 400);
        }

        var (policy, isDefault) = await VisitExecutionAccess.PolicyAsync(_policies, tenantId, cancellationToken);
        // WP-VW-W3 (W3-BE-c) — how many reps it applies to (active primary territory assignment).
        int? repCount = _reps is null ? null : (await _reps.ListAsync(tenantId, DateTimeOffset.UtcNow, cancellationToken)).Count;
        return Response<VisitEvidencePolicyDto>.Success(VisitExecutionRules.ToDto(policy, isDefault) with { RepCount = repCount });
    }
}
