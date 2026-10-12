using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.VisitPlanning.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitPlanning.Handlers.CommandHandlers;

/// <summary>Creates a staging session (born <c>draft</c>). TenantId is server-resolved; the ResourceId is a plain string
/// (no fake FK). Selection may be empty and filled later.</summary>
public sealed class CreatePlanningSessionHandler : IRequestHandler<CreatePlanningSessionCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IPlanningSessionRepository _repository;
    private readonly ICallerScope _caller;
    private readonly IUserDisplayNameResolver _userNames;
    private readonly IStrategyTemplateProductReferenceValidator? _products;

    public CreatePlanningSessionHandler(
        ITenantContext tenant, IActorContext actor, IPlanningSessionRepository repository,
        ICallerScope caller, IUserDisplayNameResolver userNames,
        // WP-VP-3C — the MDM Global Product proof of a picked product (fail-closed).
        IStrategyTemplateProductReferenceValidator? products = null)
    {
        _products = products;
        _caller = caller;
        _userNames = userNames;
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<Guid>> Handle(CreatePlanningSessionCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        if (request.CyclePeriodId == Guid.Empty)
        {
            return Response<Guid>.Fail("CyclePeriodId is required.", 400);
        }

        // WP-VP-2 (B-1, K-1) — the rep plans their OWN week: the resource is the caller. Only a read-all holder may plan
        // for another rep; a different resource from anyone else is 403 resource_not_caller.
        var (resourceAllowed, resourceId) = _caller.ResolveWriteResource(VisitPlanningPermissions.ReadAll, request.ResourceId);
        if (!resourceAllowed || resourceId is null)
        {
            return Response<Guid>.Fail(
                new[] { VisitOwnership.ResourceNotCaller, "A plan can only be created for the signed-in resource." }, 403);
        }

        // WP-VP-3A (MK-3, D3) — ONE active (not archived) plan per tenant + rep + period. Older duplicates stay readable
        // (no migration); the rule applies to a NEW plan. The answer carries the existing plan's id (data + errors[2]).
        var existing = (await _repository.ListByPeriodAndResourceAsync(
                tenantId, request.CyclePeriodId, resourceId, cancellationToken))
            .Where(s => s.TenantId == tenantId && s.CyclePeriodId == request.CyclePeriodId
                        && string.Equals(s.ResourceId, resourceId, StringComparison.OrdinalIgnoreCase)
                        && !s.IsArchived())
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefault();
        if (existing is not null)
        {
            return new Response<Guid>
            {
                Data = existing.Id,
                Errors = new[]
                {
                    PlanningSessionErrorCodes.SessionExists,
                    "This rep already has a plan for this period; open it instead.",
                    existing.Id.ToString()
                },
                StatusCode = 409,
                IsSuccessful = false
            };
        }

        // WP-VP-3C (K-7, S-4) — a product pick given on create is checked like on update.
        if (await PlanningSessionProductPick.ValidateAsync<Guid>(
                request.SelectedContacts, Array.Empty<PlanningSessionSelectedContact>(), _products, cancellationToken)
            is { } refused)
        {
            return refused;
        }

        var now = DateTimeOffset.UtcNow;
        var actor = _actor.ActorName;

        var session = new PlanningSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CyclePeriodId = request.CyclePeriodId,
            ResourceId = resourceId,
            ResourceType = _caller.HasPermission(VisitPlanningPermissions.ReadAll)
                ? PlanningSessionResourceTypes.Normalize(request.ResourceType)
                : PlanningSessionResourceTypes.User,
            // The name comes from the user directory, never from the client.
            ResourceDisplayName = await Features.PlannedVisit.Provenance.PlannedVisitProvenance.ResourceDisplayNameAsync(
                _userNames, resourceId, null, cancellationToken),
            Status = PlanningSessionStatus.Draft,
            // WP-VP-2 (B-3, K-3 / K-4) — segment / campaign / strategy are DERIVED per doctor at planning time; the
            // request's values are ignored and never stored.
            Selection = BuildSelection(
                request.SelectedAccountIds, request.SelectedPharmacyIds, request.SelectedContacts, null, null),
            Provenance = new PlanningSessionProvenance
            {
                DecidedAt = now,
                DecidedBy = actor
            },
            TargetWeekStart = string.IsNullOrWhiteSpace(request.TargetWeekStart) ? null : request.TargetWeekStart.Trim(),
            CreatedAt = now,
            CreatedBy = actor
        };

        await _repository.InsertAsync(session, cancellationToken);
        return Response<Guid>.Success(session.Id, 201);
    }

    internal static PlanningSessionSelection BuildSelection(
        IReadOnlyList<Guid>? accounts, IReadOnlyList<Guid>? pharmacies,
        IReadOnlyList<SelectedContactInput>? contacts, Guid? segmentId, Guid? campaignId) => new()
    {
        SelectedAccountIds = (accounts ?? Array.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList(),
        SelectedPharmacyIds = (pharmacies ?? Array.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList(),
        SelectedContacts = (contacts ?? Array.Empty<SelectedContactInput>())
            .Where(c => c.ContactId != Guid.Empty)
            .Select(c => new PlanningSessionSelectedContact
            {
                ContactId = c.ContactId,
                AccountId = c.AccountId,
                AccountContactLinkId = c.AccountContactLinkId,
                Products = PlanningSessionProductPick.Normalize(c.Products) ?? new List<PlanningSessionSelectedProduct>()
            })
            .ToList(),
        SegmentId = segmentId,
        CampaignId = campaignId
    };
}

/// <summary>Edits selection + optionally moves the status FORWARD. A backward / same-rank move is refused (§12; no
/// reverse transition). committed / archived sessions no longer accept a selection edit.</summary>
public sealed class UpdatePlanningSessionSelectionHandler
    : IRequestHandler<UpdatePlanningSessionSelectionCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IPlanningSessionRepository _repository;
    private readonly ICallerScope _caller;
    private readonly IStrategyTemplateProductReferenceValidator? _products;
    private readonly Features.CyclePeriod.Read.ICyclePeriodReader? _periods;
    private readonly PlanningWorkingCalendar? _calendar;
    private readonly ICycleCapacityRepository? _capacities;
    private readonly TimeProvider _clock;

    public UpdatePlanningSessionSelectionHandler(
        ITenantContext tenant, IActorContext actor, IPlanningSessionRepository repository, ICallerScope caller,
        // WP-VP-3C — the MDM Global Product proof of a picked product (fail-closed).
        IStrategyTemplateProductReferenceValidator? products = null,
        // WP-VP-4E — the day pins' week / working-day checks: the period, its working calendar (country from the
        // period's capacity, as the engine asks it) and "today".
        Features.CyclePeriod.Read.ICyclePeriodReader? periods = null,
        PlanningWorkingCalendar? calendar = null,
        ICycleCapacityRepository? capacities = null,
        TimeProvider? clock = null)
    {
        _products = products;
        _periods = periods;
        _calendar = calendar;
        _capacities = capacities;
        _clock = clock ?? TimeProvider.System;
        _caller = caller;
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(
        UpdatePlanningSessionSelectionCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var session = await _repository.GetByIdAsync(tenantId, request.PlanningSessionId, cancellationToken);
        // WP-VP-2 (B-1) — another rep's session is as absent as a missing one.
        if (session is null || !_caller.MayAccess(VisitPlanningPermissions.ReadAll, session.ResourceId))
        {
            return Response<bool>.Fail("Planning session not found.", 404);
        }

        if (session.IsCommitted() || session.IsArchived())
        {
            return Response<bool>.Fail(
                $"A {session.Status} session cannot have its selection edited.", 409);
        }

        // WP-VP-2 (B-3) — the request's segment / campaign / strategy are ignored: whatever an older record stored is kept
        // as read-only history (and no longer used), nothing new is written.
        // WP-VP-FIX-2 (D9) — each list is independent: null = leave it as it is (the Edit form sends none, so changing the
        // week no longer wipes the targets); [] = an explicit clear; a list = the new selection.
        // WP-VP-3C (K-7, S-4) — the per-doctor product pick rides on this update: checked before anything changes.
        if (await PlanningSessionProductPick.ValidateAsync<bool>(
                request.SelectedContacts, session.Selection.SelectedContacts, _products, cancellationToken) is { } refused)
        {
            return refused;
        }

        // WP-VP-4E — one draft week's day pins ride on this update too (null = keep every week's pins).
        if (request.DayPins is { } dayPins)
        {
            if (_periods is null || await _periods.GetByIdAsync(session.CyclePeriodId, cancellationToken) is not { } period)
            {
                return Response<bool>.Fail(new[]
                {
                    PlanningSessionErrorCodes.InvalidWeek, "The plan's period cannot be read; the day pins were not changed."
                }, 400);
            }

            var periodStart = DateOnly.FromDateTime(period.StartDate.UtcDateTime);
            var periodEnd = DateOnly.FromDateTime(period.EndDate.UtcDateTime);
            Func<DateOnly, string> kindOf = d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                ? PlanningDayKinds.Weekend : PlanningDayKinds.Working;
            if (_calendar is not null)
            {
                var capacity = _capacities is null ? null
                    : await _capacities.GetByCyclePeriodAsync(tenantId, session.CyclePeriodId, cancellationToken);
                var calendar = await _calendar.ResolveAsync(
                    period, capacity?.CalendarCountryCode, periodStart, periodEnd, cancellationToken);
                kindOf = calendar.KindOf;
            }

            var (pinRefused, pinWeek, pins) = PlanningDayPins.Validate<bool>(
                session, dayPins, periodStart, periodEnd, PlanningWeekCalendar.Today(_clock.GetUtcNow()), kindOf);
            if (pinRefused is not null)
            {
                return pinRefused;
            }

            session.DayPins = PlanningDayPins.Replace(session.DayPins, pinWeek, pins);
        }

        session.Selection = MergeSelection(session.Selection, request);
        if (!string.IsNullOrWhiteSpace(request.TargetWeekStart))
            session.TargetWeekStart = request.TargetWeekStart.Trim();

        if (!string.IsNullOrWhiteSpace(request.RequestedStatus))
        {
            var target = PlanningSessionStatus.Normalize(request.RequestedStatus);
            if (!PlanningSessionStatus.IsKnown(target)
                || (!string.Equals(target, session.Status, StringComparison.Ordinal)
                    && !PlanningSessionStatus.CanTransition(session.Status, target)))
            {
                return Response<bool>.Fail(
                    $"Cannot transition session from '{session.Status}' to '{target}'.", 409);
            }

            // Committing is only ever reached through Apply (it writes the atoms); a bare status flip cannot commit.
            if (string.Equals(target, PlanningSessionStatus.Committed, StringComparison.Ordinal))
            {
                return Response<bool>.Fail("Use apply to commit a session.", 409);
            }

            // WP-VP-3A (D3) — "delete an empty draft" = archive it, and ONLY a plan without targets and without an
            // approved week (after this request's selection change).
            if (string.Equals(target, PlanningSessionStatus.Archived, StringComparison.Ordinal)
                && (session.HasTargets() || session.HasApprovedWeek()))
            {
                return Response<bool>.Fail(
                    new[]
                    {
                        PlanningSessionErrorCodes.SessionNotEmpty,
                        "Only a plan without targets and without an approved week can be archived."
                    },
                    409);
            }

            session.Status = target;
        }

        session.UpdatedAt = DateTimeOffset.UtcNow;
        session.UpdatedBy = _actor.ActorName;

        var expectedVersion = request.ExpectedVersion ?? session.Version;
        var ok = await _repository.ReplaceAsync(session, expectedVersion, cancellationToken);
        return ok
            ? Response<bool>.Success(true, 200)
            : Response<bool>.Fail("The session was modified concurrently; reload and retry.", 409);
    }

    /// <summary>WP-VP-FIX-2 (D9) — the selection after an update: a list the request leaves null keeps its current value;
    /// an empty or filled list replaces it (normalised exactly as on create). The stored segment / campaign are kept as
    /// read-only history (WP-VP-2 B-3).</summary>
    internal static PlanningSessionSelection MergeSelection(
        PlanningSessionSelection current, UpdatePlanningSessionSelectionCommand request)
    {
        var requested = CreatePlanningSessionHandler.BuildSelection(
            request.SelectedAccountIds, request.SelectedPharmacyIds, request.SelectedContacts,
            current.SegmentId, current.CampaignId);
        return new PlanningSessionSelection
        {
            SelectedAccountIds = request.SelectedAccountIds is null ? current.SelectedAccountIds : requested.SelectedAccountIds,
            SelectedPharmacyIds = request.SelectedPharmacyIds is null ? current.SelectedPharmacyIds : requested.SelectedPharmacyIds,
            // WP-VP-3C — a doctor whose Products is null keeps its stored pick (D9 per doctor); [] clears it.
            SelectedContacts = request.SelectedContacts is null
                ? current.SelectedContacts
                : requested.SelectedContacts
                    .Select(c =>
                    {
                        var input = request.SelectedContacts.First(i => i.ContactId == c.ContactId && i.AccountId == c.AccountId);
                        c.Products = PlanningSessionProductPick.Merge(input, current.SelectedContacts);
                        return c;
                    })
                    .ToList(),
            SegmentId = current.SegmentId,
            CampaignId = current.CampaignId
        };
    }
}

/// <summary>Applies the session: generate → write FU01 atoms atomically → flip to <c>committed</c> (D-APPLY-ATOMICITY =
/// C). A mid-apply failure leaves no half-plan and does NOT flip the session (the unit of work is all-or-nothing).</summary>
public sealed class ApplyPlanningSessionHandler
    : IRequestHandler<ApplyPlanningSessionCommand, Response<VisitPlanApplyResult>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IPlanningSessionRepository _repository;
    private readonly IPlanningSessionApplyUnitOfWork _unitOfWork;
    private readonly VisitPlanningEngine _engine;
    private readonly ICallerScope _caller;
    private readonly TimeProvider _clock;

    public ApplyPlanningSessionHandler(
        ITenantContext tenant,
        IActorContext actor,
        IPlanningSessionRepository repository,
        IPlanningSessionApplyUnitOfWork unitOfWork,
        VisitPlanningEngine engine,
        ICallerScope caller,
        // WP-VP-3A — "today" (UTC) for the week rules; the system clock unless a test pins it.
        TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
        _caller = caller;
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _engine = engine;
    }

    public async Task<Response<VisitPlanApplyResult>> Handle(
        ApplyPlanningSessionCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitPlanApplyResult>.Fail("Tenant context is required.", 400);
        }

        var session = await _repository.GetByIdAsync(tenantId, request.PlanningSessionId, cancellationToken);
        if (session is null || !_caller.MayAccess(VisitPlanningPermissions.ReadAll, session.ResourceId))
        {
            return Response<VisitPlanApplyResult>.Fail("Planning session not found.", 404);
        }

        // WP-VP-FIX-1 (D2) — a second apply of an already-committed plan is refused with a machine code (the atoms exist;
        // re-plan is the only path that touches them). Every other non-forward status keeps its plain 409.
        if (session.IsCommitted())
        {
            return Response<VisitPlanApplyResult>.Fail(
                new[] { PlanningSessionErrorCodes.AlreadyCommitted, "This plan is already committed; it cannot be applied again." },
                409);
        }

        if (!PlanningSessionStatus.CanTransition(session.Status, PlanningSessionStatus.Committed))
        {
            return Response<VisitPlanApplyResult>.Fail(
                $"A {session.Status} session cannot be applied.", 409);
        }

        if (!string.IsNullOrWhiteSpace(request.WeekStart))
        {
            return await ApproveWeekAsync(session, request, cancellationToken);
        }

        // WP-VP-3A — DEPRECATED path, to be removed in Faz 4 (when the Web moves to per-week approval): apply without a
        // weekStart writes every draft week and commits the session, exactly as before.
        // "Save as this week's plan": the manual order from the request (else the session's persisted order) drives the
        // atoms and is persisted on the session. Null ⇒ the engine optimum.
        var manualOrder = request.ManualVisitOrder ?? (session.ManualVisitOrder.Count > 0 ? session.ManualVisitOrder : null);
        var options = new VisitPlanGenerationOptions(
            request.VisitPurpose, request.VisitType, null, request.StartLat, request.StartLong,
            EffectiveAt: _clock.GetUtcNow(), ManualVisitOrder: manualOrder);
        var build = await _engine.BuildApplyAsync(session, options, cancellationToken);
        if (!build.Success || build.Preview is null)
        {
            return Response<VisitPlanApplyResult>.Fail(build.Error ?? "Apply generation failed.", 400);
        }

        var atoms = build.Atoms;
        var preview = build.Preview;
        var now = DateTimeOffset.UtcNow;

        // Flip the session (in memory) so the write + the flip are one atomic operation in the unit of work.
        session.Status = PlanningSessionStatus.Committed;
        session.ManualVisitOrder = manualOrder?.ToList() ?? new List<Guid>(); // persist the applied order (empty = optimum)
        session.CommittedPlannedVisitIds = atoms.Select(a => a.Id).ToList();
        session.GenerationState = new PlanningSessionGenerationState
        {
            LastGeneratedAt = now,
            ScheduledCount = preview.Scheduled.Count,
            UnscheduledCount = preview.Unscheduled.Count,
            SupplyDemandStatus = preview.SupplyDemand.Status
        };
        session.UpdatedAt = now;
        session.UpdatedBy = _actor.ActorName;

        var expectedVersion = request.ExpectedVersion ?? session.Version;
        var committed = await _unitOfWork.ApplyAsync(session, expectedVersion, atoms, cancellationToken);
        if (!committed)
        {
            return Response<VisitPlanApplyResult>.Fail(
                "The session was modified concurrently; reload and retry.", 409);
        }

        return Response<VisitPlanApplyResult>.Success(
            new VisitPlanApplyResult(
                session.Id, session.Status, session.CommittedPlannedVisitIds,
                preview.Scheduled.Count, preview.Unscheduled.Count),
            200);
    }

    /// <summary>
    /// WP-VP-3A — "approve the week": only that week's visits become atoms, the week is stored as approved (from
    /// nothing, or from reopened) with an <c>approve</c> history entry, and the session is NOT committed (the period plan
    /// stays open). Atoms + session are written in the same all-or-nothing unit of work as apply.
    /// </summary>
    private async Task<Response<VisitPlanApplyResult>> ApproveWeekAsync(
        PlanningSession session, ApplyPlanningSessionCommand request, CancellationToken cancellationToken)
    {
        var existingWeek = session.WeekOf(request.WeekStart!.Trim());
        var manualOrder = request.ManualVisitOrder ?? (existingWeek?.ManualVisitOrder.Count > 0 ? existingWeek.ManualVisitOrder : null);
        var now = _clock.GetUtcNow();
        var options = new VisitPlanGenerationOptions(
            request.VisitPurpose, request.VisitType, null, request.StartLat, request.StartLong,
            EffectiveAt: now, ManualVisitOrder: manualOrder);
        var build = await _engine.BuildApplyAsync(session, options, cancellationToken, request.WeekStart.Trim());
        if (!build.Success || build.Preview is null || build.WeekStart is null)
        {
            return build.ErrorCode is { } code
                ? Response<VisitPlanApplyResult>.Fail(new[] { code, build.Error ?? code }, build.StatusCode)
                : Response<VisitPlanApplyResult>.Fail(build.Error ?? "Apply generation failed.", 400);
        }

        var actor = _actor.ActorName;
        var week = session.WeekOf(build.WeekStart);
        if (week is null)
        {
            week = new PlanningWeek { WeekStart = build.WeekStart };
            session.Weeks.Add(week);
        }

        var atomIds = build.Atoms.Select(a => a.Id).ToList();
        week.Status = PlanningWeekStatus.Approved;
        week.ApprovedAt = now;
        week.ApprovedBy = actor;
        // the visits a reopened week kept (reported) + this approval's atoms
        week.PlannedVisitIds = (build.KeptVisitIds ?? Array.Empty<Guid>()).Concat(atomIds).Distinct().ToList();
        week.ManualVisitOrder = manualOrder?.ToList() ?? new List<Guid>();
        week.History.Add(new PlanningWeekHistoryEntry { At = now, By = actor, Action = PlanningWeekActions.Approve });

        var weekNumber = build.Preview.Weeks?.ToList().FindIndex(w => w.WeekStart == build.WeekStart) ?? -1;
        session.GenerationState = new PlanningSessionGenerationState
        {
            LastGeneratedAt = now,
            ScheduledCount = build.Atoms.Count,
            UnscheduledCount = build.Preview.Unscheduled.Count(u => u.WeekNumber == weekNumber),
            SupplyDemandStatus = build.Preview.SupplyDemand.Status
        };
        session.UpdatedAt = now;
        session.UpdatedBy = actor;

        var expectedVersion = request.ExpectedVersion ?? session.Version;
        if (!await _unitOfWork.ApplyAsync(session, expectedVersion, build.Atoms, cancellationToken))
        {
            return Response<VisitPlanApplyResult>.Fail("The session was modified concurrently; reload and retry.", 409);
        }

        return Response<VisitPlanApplyResult>.Success(
            new VisitPlanApplyResult(
                session.Id, session.Status, atomIds, build.Atoms.Count, session.GenerationState.UnscheduledCount,
                build.WeekStart, week.Status),
            200);
    }
}

/// <summary>
/// WP-VP-3A (MK-4) — reopens an APPROVED week of the plan. A reason of at least 10 characters is required (400
/// <c>reopen_reason_required</c>); the week must be approved (409 <c>week_not_approved</c>) and not over (409
/// <c>week_in_past</c>). Its visits WITHOUT a report are cancelled with the reason code <c>week_reopened</c> (the
/// existing <c>PlannedVisit.CancellationReason</c> + <c>cancelled</c> status, the cancel command's pattern); visits with a
/// report stay and count as fixed when the week is approved again. The week becomes <c>reopened</c> with a
/// <c>reopen</c> history entry. Cancellations + session are ONE unit of work.
/// <para>A NEW write command (the only one of WP-VP-3A): the architecture test's unaudited CRM list goes 26 → 27; it is
/// wired to the central audit in Faz 8 (AUD-001 = A, last).</para>
/// </summary>
public sealed class ReopenPlanningWeekHandler
    : IRequestHandler<ReopenPlanningWeekCommand, Response<PlanningWeekReopenResult>>
{
    public const int MinReasonLength = 10;

    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IPlanningSessionRepository _repository;
    private readonly IPlannedVisitRepository _plannedVisits;
    private readonly IVisitReportRepository _reports;
    private readonly Features.CyclePeriod.Read.ICyclePeriodReader _periods;
    private readonly IPlanningSessionApplyUnitOfWork _unitOfWork;
    private readonly ICallerScope _caller;
    private readonly TimeProvider _clock;

    public ReopenPlanningWeekHandler(
        ITenantContext tenant,
        IActorContext actor,
        IPlanningSessionRepository repository,
        IPlannedVisitRepository plannedVisits,
        IVisitReportRepository reports,
        Features.CyclePeriod.Read.ICyclePeriodReader periods,
        IPlanningSessionApplyUnitOfWork unitOfWork,
        ICallerScope caller,
        TimeProvider? clock = null)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
        _plannedVisits = plannedVisits;
        _reports = reports;
        _periods = periods;
        _unitOfWork = unitOfWork;
        _caller = caller;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<PlanningWeekReopenResult>> Handle(
        ReopenPlanningWeekCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<PlanningWeekReopenResult>.Fail("Tenant context is required.", 400);
        }

        var session = await _repository.GetByIdAsync(tenantId, request.PlanningSessionId, cancellationToken);
        if (session is null || !_caller.MayAccess(VisitPlanningPermissions.ReadAll, session.ResourceId))
        {
            return Response<PlanningWeekReopenResult>.Fail("Planning session not found.", 404);
        }

        var reason = (request.Reason ?? string.Empty).Trim();
        if (reason.Length < MinReasonLength)
        {
            return Response<PlanningWeekReopenResult>.Fail(
                new[] { PlanningSessionErrorCodes.ReopenReasonRequired, $"A reason of at least {MinReasonLength} characters is required." },
                400);
        }

        var period = await _periods.GetByIdAsync(session.CyclePeriodId, cancellationToken);
        if (period is null
            || !PlanningWeekCalendar.TryParseWeek(
                request.WeekStart,
                DateOnly.FromDateTime(period.StartDate.UtcDateTime),
                DateOnly.FromDateTime(period.EndDate.UtcDateTime),
                out var span))
        {
            return Response<PlanningWeekReopenResult>.Fail(
                new[] { PlanningSessionErrorCodes.InvalidWeek, "weekStart must be a Monday (yyyy-MM-dd) of a week of the plan's period." },
                400);
        }

        var week = session.WeekOf(span.WeekStart);
        if (week is null || !week.IsApproved())
        {
            return Response<PlanningWeekReopenResult>.Fail(
                new[] { PlanningSessionErrorCodes.WeekNotApproved, "Only an approved week can be reopened." }, 409);
        }

        var now = _clock.GetUtcNow();
        if (PlanningWeekCalendar.IsPast(span, PlanningWeekCalendar.Today(now)))
        {
            return Response<PlanningWeekReopenResult>.Fail(
                new[] { PlanningSessionErrorCodes.WeekInPast, "A past week cannot be reopened." }, 409);
        }

        var expectedVersion = request.ExpectedVersion ?? session.Version;
        if (expectedVersion != session.Version)
        {
            return Response<PlanningWeekReopenResult>.Fail("The session was modified concurrently; reload and retry.", 409);
        }

        var ids = week.PlannedVisitIds.ToHashSet();
        var visits = (await _plannedVisits.ListAsync(tenantId, cancellationToken)).Where(v => ids.Contains(v.Id)).ToList();
        var reported = (await _reports.ListByPlannedVisitIdsAsync(tenantId, ids.ToList(), cancellationToken))
            .Select(r => r.PlannedVisitId)
            .ToHashSet();

        var actor = _actor.ActorName;
        var cancelled = new List<Domain.Entities.PlannedVisit>();
        var kept = new List<Guid>();
        foreach (var visit in visits)
        {
            if (visit.IsCancelled() || visit.IsArchived())
            {
                continue;
            }

            if (reported.Contains(visit.Id)
                || !Features.PlannedVisit.PlannedVisitValidation.IsTransitionAllowed(visit.PlanStatus, PlannedVisitStatus.Cancelled))
            {
                kept.Add(visit.Id);
                continue;
            }

            visit.PlanStatus = PlannedVisitStatus.Cancelled;
            visit.CancellationReason = PlanningSessionErrorCodes.WeekReopenedCancellationReason;
            visit.UpdatedAt = now;
            visit.UpdatedBy = actor;
            cancelled.Add(visit);
        }

        week.Status = PlanningWeekStatus.Reopened;
        week.History.Add(new PlanningWeekHistoryEntry
        {
            At = now, By = actor, Action = PlanningWeekActions.Reopen, Reason = reason
        });
        session.UpdatedAt = now;
        session.UpdatedBy = actor;

        if (!await _unitOfWork.ReopenWeekAsync(session, expectedVersion, cancelled, cancellationToken))
        {
            return Response<PlanningWeekReopenResult>.Fail("The session was modified concurrently; reload and retry.", 409);
        }

        return Response<PlanningWeekReopenResult>.Success(new PlanningWeekReopenResult(
            session.Id, span.WeekStart, week.Status, cancelled.Select(v => v.Id).ToList(), kept));
    }
}

/// <summary>WP-VP-FIX-1 — machine codes of the planning-session refusals (first entry of <c>errors[]</c>, message second —
/// the claims v2 convention).</summary>
public static class PlanningSessionErrorCodes
{
    public const string AlreadyCommitted = "planning_session_already_committed";

    // WP-VP-3A
    public const string SessionExists = "planning_session_exists";
    public const string SessionNotEmpty = "planning_session_not_empty";
    public const string InvalidWeek = "invalid_week";
    public const string WeekInPast = "week_in_past";
    public const string WeekAlreadyApproved = "week_already_approved";
    public const string WeekNotApproved = "week_not_approved";
    public const string ReopenReasonRequired = "reopen_reason_required";

    /// <summary>The cancellation reason a reopened week writes on the visits it cancels.</summary>
    public const string WeekReopenedCancellationReason = "week_reopened";
}

/// <summary>Re-plans a subset in place (D-REPLAN = A): re-runs the route for the affected contacts and replaces ONLY
/// their atoms; the rest are untouched and the session is not reopened.</summary>
public sealed class ReplanPlanningSessionHandler
    : IRequestHandler<ReplanPlanningSessionCommand, Response<VisitPlanApplyResult>>
{
    private readonly ITenantContext _tenant;
    private readonly IPlanningSessionRepository _repository;
    private readonly IPlanningSessionApplyUnitOfWork _unitOfWork;
    private readonly VisitPlanningEngine _engine;
    private readonly ICallerScope _caller;

    public ReplanPlanningSessionHandler(
        ITenantContext tenant,
        IPlanningSessionRepository repository,
        IPlanningSessionApplyUnitOfWork unitOfWork,
        VisitPlanningEngine engine,
        ICallerScope caller)
    {
        _caller = caller;
        _tenant = tenant;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _engine = engine;
    }

    public async Task<Response<VisitPlanApplyResult>> Handle(
        ReplanPlanningSessionCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitPlanApplyResult>.Fail("Tenant context is required.", 400);
        }

        if (request.AffectedContactIds is null || request.AffectedContactIds.Count == 0)
        {
            return Response<VisitPlanApplyResult>.Fail("At least one affected contact is required.", 400);
        }

        var session = await _repository.GetByIdAsync(tenantId, request.PlanningSessionId, cancellationToken);
        if (session is null || !_caller.MayAccess(VisitPlanningPermissions.ReadAll, session.ResourceId))
        {
            return Response<VisitPlanApplyResult>.Fail("Planning session not found.", 404);
        }

        if (!session.IsCommitted())
        {
            return Response<VisitPlanApplyResult>.Fail(
                "Only a committed session can be re-planned (its atoms are updated in place).", 409);
        }

        var manualOrder = request.ManualVisitOrder ?? (session.ManualVisitOrder.Count > 0 ? session.ManualVisitOrder : null);
        var options = new VisitPlanGenerationOptions(
            request.VisitPurpose, request.VisitType, null, request.StartLat, request.StartLong, ManualVisitOrder: manualOrder);
        var build = await _engine.BuildReplanAsync(
            session, request.AffectedContactIds, options, cancellationToken);
        if (!build.Success)
        {
            return Response<VisitPlanApplyResult>.Fail(build.Error ?? "Re-plan generation failed.", 400);
        }

        await _unitOfWork.ReplanAsync(build.UpdatedAtoms, cancellationToken);

        return Response<VisitPlanApplyResult>.Success(
            new VisitPlanApplyResult(
                session.Id, session.Status, session.CommittedPlannedVisitIds,
                build.UpdatedAtoms.Count, 0),
            200);
    }
}
