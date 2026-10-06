using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
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

    public CreatePlanningSessionHandler(
        ITenantContext tenant, IActorContext actor, IPlanningSessionRepository repository,
        ICallerScope caller, IUserDisplayNameResolver userNames)
    {
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
                AccountContactLinkId = c.AccountContactLinkId
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

    public UpdatePlanningSessionSelectionHandler(
        ITenantContext tenant, IActorContext actor, IPlanningSessionRepository repository, ICallerScope caller)
    {
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
        session.Selection = CreatePlanningSessionHandler.BuildSelection(
            request.SelectedAccountIds, request.SelectedPharmacyIds, request.SelectedContacts,
            session.Selection.SegmentId, session.Selection.CampaignId);
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

    public ApplyPlanningSessionHandler(
        ITenantContext tenant,
        IActorContext actor,
        IPlanningSessionRepository repository,
        IPlanningSessionApplyUnitOfWork unitOfWork,
        VisitPlanningEngine engine,
        ICallerScope caller)
    {
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

        // "Save as this week's plan": the manual order from the request (else the session's persisted order) drives the
        // atoms and is persisted on the session. Null ⇒ the engine optimum.
        var manualOrder = request.ManualVisitOrder ?? (session.ManualVisitOrder.Count > 0 ? session.ManualVisitOrder : null);
        var options = new VisitPlanGenerationOptions(
            request.VisitPurpose, request.VisitType, null, request.StartLat, request.StartLong, ManualVisitOrder: manualOrder);
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
}

/// <summary>WP-VP-FIX-1 — machine codes of the planning-session refusals (first entry of <c>errors[]</c>, message second —
/// the claims v2 convention).</summary>
public static class PlanningSessionErrorCodes
{
    public const string AlreadyCommitted = "planning_session_already_committed";
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
