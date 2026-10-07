using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitPlanning.Queries;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitPlanning.Handlers.QueryHandlers;

/// <summary>Dry-run preview (①–⑦). Persists NOTHING — no atom write, no session status change, and the transient
/// SupplyDemandSummary is never stored (D-SUPPLY-DEMAND-SHAPE = A).</summary>
public sealed class GeneratePlanPreviewHandler : IRequestHandler<GeneratePlanPreviewQuery, Response<VisitPlanPreview>>
{
    private readonly ITenantContext _tenant;
    private readonly IPlanningSessionRepository _repository;
    private readonly VisitPlanningEngine _engine;
    private readonly ICallerScope _caller;

    public GeneratePlanPreviewHandler(
        ITenantContext tenant, IPlanningSessionRepository repository, VisitPlanningEngine engine, ICallerScope caller)
    {
        _caller = caller;
        _tenant = tenant;
        _repository = repository;
        _engine = engine;
    }

    public async Task<Response<VisitPlanPreview>> Handle(
        GeneratePlanPreviewQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitPlanPreview>.Fail("Tenant context is required.", 400);
        }

        var session = await _repository.GetByIdAsync(tenantId, request.PlanningSessionId, cancellationToken);
        if (session is null || !_caller.MayAccess(VisitPlanningPermissions.ReadAll, session.ResourceId))
        {
            return Response<VisitPlanPreview>.Fail("Planning session not found.", 404);
        }

        // The manual order comes from the request (a live reorder); if absent, fall back to any order persisted on the
        // session, so a committed manual plan re-previews in its saved sequence. Null ⇒ the engine optimum.
        var manualOrder = request.ManualVisitOrder ?? (session.ManualVisitOrder.Count > 0 ? session.ManualVisitOrder : null);
        var options = new VisitPlanGenerationOptions(
            request.VisitPurpose, request.VisitType, null, request.StartLat, request.StartLong, ManualVisitOrder: manualOrder);
        var outcome = await _engine.PreviewAsync(session, options, cancellationToken);
        return outcome.Success
            ? Response<VisitPlanPreview>.Success(outcome.Preview!, 200)
            : Response<VisitPlanPreview>.Fail(outcome.Error ?? "Preview failed.", 400);
    }
}

public sealed class ListPlanningSessionsHandler
    : IRequestHandler<ListPlanningSessionsQuery, Response<PlanningSessionListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IPlanningSessionRepository _repository;

    private readonly ICallerScope _caller;

    public ListPlanningSessionsHandler(ITenantContext tenant, IPlanningSessionRepository repository, ICallerScope caller)
    {
        _caller = caller;
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<Response<PlanningSessionListDto>> Handle(
        ListPlanningSessionsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<PlanningSessionListDto>.Fail("Tenant context is required.", 400);
        }

        var rows = await _repository.ListAsync(tenantId, cancellationToken);

        // WP-VP-2 (B-1) — without read-all only the caller's own sessions are listed.
        IEnumerable<Domain.Entities.PlanningSession> filtered = rows.Where(s => _caller.MayAccess(VisitPlanningPermissions.ReadAll, s.ResourceId));
        if (request.CyclePeriodId is { } periodId && periodId != Guid.Empty)
        {
            filtered = filtered.Where(s => s.CyclePeriodId == periodId);
        }

        if (!string.IsNullOrWhiteSpace(request.ResourceId))
        {
            filtered = filtered.Where(s =>
                string.Equals(s.ResourceId, request.ResourceId!.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status!.Trim().ToLowerInvariant();
            filtered = filtered.Where(s => string.Equals(s.Status, status, StringComparison.Ordinal));
        }

        var items = filtered.Select(PlanningSessionMapper.ToListItem).ToList();
        return Response<PlanningSessionListDto>.Success(new PlanningSessionListDto(items, items.Count), 200);
    }
}

public sealed class GetPlanningSessionByIdHandler
    : IRequestHandler<GetPlanningSessionByIdQuery, Response<PlanningSessionDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IPlanningSessionRepository _repository;

    private readonly ICallerScope _caller;
    private readonly Features.PlannedVisit.VisitTargetNameReader _names;
    private readonly Features.CyclePeriod.Read.ICyclePeriodReader? _periods;
    private readonly TimeProvider _clock;

    public GetPlanningSessionByIdHandler(
        ITenantContext tenant, IPlanningSessionRepository repository, ICallerScope caller,
        Features.PlannedVisit.VisitTargetNameReader names,
        // WP-VP-3A — the period (for its weeks) and "today"; without the reader the detail carries no weeks.
        Features.CyclePeriod.Read.ICyclePeriodReader? periods = null,
        TimeProvider? clock = null)
    {
        _periods = periods;
        _clock = clock ?? TimeProvider.System;
        _tenant = tenant;
        _repository = repository;
        _caller = caller;
        _names = names;
    }

    public async Task<Response<PlanningSessionDto>> Handle(
        GetPlanningSessionByIdQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<PlanningSessionDto>.Fail("Tenant context is required.", 400);
        }

        var session = await _repository.GetByIdAsync(tenantId, request.PlanningSessionId, cancellationToken);
        if (session is null || !_caller.MayAccess(VisitPlanningPermissions.ReadAll, session.ResourceId))
        {
            return Response<PlanningSessionDto>.Fail("Planning session not found.", 404);
        }

        // WP-VP-2 (B-8) — names of every selected doctor / account / pharmacy, one read per master.
        var selection = session.Selection;
        var names = await _names.ReadAsync(
            tenantId,
            selection.SelectedAccountIds.Concat(selection.SelectedPharmacyIds)
                .Concat(selection.SelectedContacts.Select(c => c.AccountId ?? Guid.Empty))
                .Select(id => (Guid?)id),
            selection.SelectedContacts.Select(c => (Guid?)c.ContactId),
            cancellationToken);
        var dto = PlanningSessionMapper.ToDto(session, names);
        if (_periods is not null && await _periods.GetByIdAsync(session.CyclePeriodId, cancellationToken) is { } period)
        {
            dto = dto with
            {
                Weeks = PlanningSessionMapper.DetailWeeks(
                    session,
                    DateOnly.FromDateTime(period.StartDate.UtcDateTime),
                    DateOnly.FromDateTime(period.EndDate.UtcDateTime),
                    PlanningWeekCalendar.Today(_clock.GetUtcNow()))
            };
        }

        return Response<PlanningSessionDto>.Success(dto, 200);
    }
}
