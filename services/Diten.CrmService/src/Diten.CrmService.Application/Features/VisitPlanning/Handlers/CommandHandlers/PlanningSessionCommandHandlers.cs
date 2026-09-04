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

    public CreatePlanningSessionHandler(
        ITenantContext tenant, IActorContext actor, IPlanningSessionRepository repository)
    {
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

        if (string.IsNullOrWhiteSpace(request.ResourceId))
        {
            return Response<Guid>.Fail("ResourceId is required.", 400);
        }

        var now = DateTimeOffset.UtcNow;
        var actor = _actor.ActorName;

        var session = new PlanningSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CyclePeriodId = request.CyclePeriodId,
            ResourceId = request.ResourceId.Trim(),
            ResourceType = PlanningSessionResourceTypes.Normalize(request.ResourceType),
            ResourceDisplayName = string.IsNullOrWhiteSpace(request.ResourceDisplayName)
                ? null : request.ResourceDisplayName.Trim(),
            Status = PlanningSessionStatus.Draft,
            Selection = BuildSelection(
                request.SelectedAccountIds, request.SelectedPharmacyIds, request.SelectedContacts,
                request.SegmentId, request.CampaignId),
            Provenance = new PlanningSessionProvenance
            {
                SegmentId = request.SegmentId,
                CampaignId = request.CampaignId,
                StrategyTemplateId = request.StrategyTemplateId,
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

    public UpdatePlanningSessionSelectionHandler(
        ITenantContext tenant, IActorContext actor, IPlanningSessionRepository repository)
    {
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
        if (session is null)
        {
            return Response<bool>.Fail("Planning session not found.", 404);
        }

        if (session.IsCommitted() || session.IsArchived())
        {
            return Response<bool>.Fail(
                $"A {session.Status} session cannot have its selection edited.", 409);
        }

        session.Selection = CreatePlanningSessionHandler.BuildSelection(
            request.SelectedAccountIds, request.SelectedPharmacyIds, request.SelectedContacts,
            request.SegmentId, request.CampaignId);
        session.Provenance.SegmentId = request.SegmentId;
        session.Provenance.CampaignId = request.CampaignId;
        session.Provenance.StrategyTemplateId = request.StrategyTemplateId;
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

    public ApplyPlanningSessionHandler(
        ITenantContext tenant,
        IActorContext actor,
        IPlanningSessionRepository repository,
        IPlanningSessionApplyUnitOfWork unitOfWork,
        VisitPlanningEngine engine)
    {
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
        if (session is null)
        {
            return Response<VisitPlanApplyResult>.Fail("Planning session not found.", 404);
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

/// <summary>Re-plans a subset in place (D-REPLAN = A): re-runs the route for the affected contacts and replaces ONLY
/// their atoms; the rest are untouched and the session is not reopened.</summary>
public sealed class ReplanPlanningSessionHandler
    : IRequestHandler<ReplanPlanningSessionCommand, Response<VisitPlanApplyResult>>
{
    private readonly ITenantContext _tenant;
    private readonly IPlanningSessionRepository _repository;
    private readonly IPlanningSessionApplyUnitOfWork _unitOfWork;
    private readonly VisitPlanningEngine _engine;

    public ReplanPlanningSessionHandler(
        ITenantContext tenant,
        IPlanningSessionRepository repository,
        IPlanningSessionApplyUnitOfWork unitOfWork,
        VisitPlanningEngine engine)
    {
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
        if (session is null)
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
