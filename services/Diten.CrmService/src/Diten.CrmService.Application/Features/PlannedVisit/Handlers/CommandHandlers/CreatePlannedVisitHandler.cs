using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.PlannedVisit.Commands;
using Diten.CrmService.Application.Features.PlannedVisit.Contract;
using Diten.CrmService.Application.Features.PlannedVisit.Provenance;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Features.PlannedVisit.Handlers.CommandHandlers;

/// <summary>
/// Creates a plan. Order is fixed: shape → date rules → target resolution → campaign context → journey/stage validity →
/// code uniqueness → the legacy overlap / same-day guards (only when the plan is born ACTIVE) → the derived provenance
/// blocks (frequency / consent / availability / selection) → the write. Every external check completes before the
/// insert, so a dependency outage cannot leave a half-authored plan.
/// <para><b>No engine (D8).</b> Nothing here generates a plan, packs a slot, computes a duration or advances a stage.
/// The consent verdict is STORED, never enforced at create — that is the confirm gate's job (D6). Motor-filled slot
/// fields and the derived provenance blocks that arrive in the payload are ignored (V26).</para>
/// </summary>
public sealed class CreatePlannedVisitHandler : IRequestHandler<CreatePlannedVisitCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IPlannedVisitRepository _repository;
    private readonly PlannedVisitWriteGuards _guards;
    private readonly PlannedVisitJourneyProbe _journeyProbe;
    private readonly PlannedVisitFrequencyProbe _frequencyProbe;
    private readonly PlannedVisitConsentProbe _consentProbe;
    private readonly PlannedVisitAvailabilityProbe _availabilityProbe;

    public CreatePlannedVisitHandler(
        ITenantContext tenant,
        IActorContext actor,
        IPlannedVisitRepository repository,
        PlannedVisitWriteGuards guards,
        PlannedVisitJourneyProbe journeyProbe,
        PlannedVisitFrequencyProbe frequencyProbe,
        PlannedVisitConsentProbe consentProbe,
        PlannedVisitAvailabilityProbe availabilityProbe)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
        _guards = guards;
        _journeyProbe = journeyProbe;
        _frequencyProbe = frequencyProbe;
        _consentProbe = consentProbe;
        _availabilityProbe = availabilityProbe;
    }

    public async Task<Response<Guid>> Handle(CreatePlannedVisitCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var shapeFailure = PlannedVisitValidation.ValidateShape(
            request.VisitCode, request.TargetType, request.TargetId, request.ResourceId, request.ResourceType,
            request.PlannedStartTime, request.PlannedEndTime, request.PlannedDurationMinutes,
            request.VisitPurpose, request.VisitType, request.Objective, request.Notes, validateCode: true);
        if (shapeFailure is not null)
        {
            return Fail(shapeFailure);
        }

        // Source: FU01 writes only `manual` (V20); a reserved value is refused.
        var source = string.IsNullOrWhiteSpace(request.Source) ? PlannedVisitSource.Manual : request.Source!.Trim().ToLowerInvariant();
        if (!string.Equals(source, PlannedVisitSource.Manual, StringComparison.Ordinal))
        {
            return Fail(new PlannedVisitValidation.Failure(
                $"Source '{source}' is reserved; FU01 creates only manual plans.",
                PlannedVisitErrorCodes.UnsupportedVocabularyValue));
        }

        // Birth status: draft (default) or planned. confirmed/cancelled/archived are reached only through transitions.
        var status = string.IsNullOrWhiteSpace(request.PlanStatus)
            ? PlannedVisitStatus.Draft
            : PlannedVisitStatus.Normalize(request.PlanStatus);
        if (!string.Equals(status, PlannedVisitStatus.Draft, StringComparison.Ordinal)
            && !string.Equals(status, PlannedVisitStatus.Planned, StringComparison.Ordinal))
        {
            return Fail(new PlannedVisitValidation.Failure(
                "A plan can be created only as draft or planned.", PlannedVisitErrorCodes.InvalidTransition));
        }

        var plannedDate = PlannedVisitValidation.ParseDate(request.PlannedDate);
        if (plannedDate is not { } date)
        {
            return Fail(new PlannedVisitValidation.Failure("PlannedDate is required.", PlannedVisitErrorCodes.DateRequired));
        }

        // On create the planned date must not be in the past (V7/AC-TIME-3) — a draft exception applies only on update.
        if (date < DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime))
        {
            return Fail(new PlannedVisitValidation.Failure(
                "PlannedDate cannot be in the past.", PlannedVisitErrorCodes.DateInPast));
        }

        var targetResult = await _guards.ResolveTargetAsync(tenantId, request.TargetType, request.TargetId, cancellationToken);
        if (targetResult.Target is null)
        {
            return Fail(targetResult.Failure ?? new PlannedVisitValidation.Failure(
                "Target could not be resolved.", PlannedVisitErrorCodes.TargetNotFound));
        }

        if (await _guards.ValidateCampaignAsync(tenantId, request.CampaignId, cancellationToken) is { } campaignFailure)
        {
            return Fail(campaignFailure);
        }

        var journeyResult = await _journeyProbe.ResolveAsync(
            request.ContentEngagementJourneyId, request.ContentEngagementJourneyStageId,
            request.ContentSource, request.StrategyTemplateId, cancellationToken);
        if (journeyResult.Failure is { } journeyFailure)
        {
            return Fail(journeyFailure);
        }

        var code = request.VisitCode.Trim();
        var sameCode = await _repository.ListByCodeAsync(tenantId, code, cancellationToken);
        if (sameCode.Any(v => !v.IsArchived()))
        {
            return Fail(new PlannedVisitValidation.Failure(
                $"A plan already uses VisitCode '{code}'.", PlannedVisitErrorCodes.CodeTaken, 409));
        }

        var target = targetResult.Target;
        var now = DateTimeOffset.UtcNow;
        var actor = _actor.ActorName;

        var entity = new PlannedVisitEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VisitCode = code,
            TargetType = PlannedVisitTargetType.Normalize(request.TargetType),
            TargetId = request.TargetId,
            AccountId = target.AccountId,
            ContactId = target.ContactId,
            AccountContactLinkId = target.AccountContactLinkId,
            PlannedDate = date,
            PlannedStartTime = PlannedVisitValidation.Trim(request.PlannedStartTime),
            PlannedEndTime = PlannedVisitValidation.Trim(request.PlannedEndTime),
            PlannedDurationMinutes = request.PlannedDurationMinutes,
            Resource = new PlannedVisitResourceRef
            {
                ResourceId = request.ResourceId.Trim(),
                ResourceType = PlannedVisitResourceTypes.Normalize(request.ResourceType),
                DisplayName = PlannedVisitValidation.Trim(request.ResourceDisplayName)
            },
            PositionCode = PlannedVisitValidation.Trim(request.PositionCode),
            PositionId = request.PositionId,
            VisitPurpose = PlannedVisitPurpose.Normalize(request.VisitPurpose),
            VisitType = PlannedVisitType.Normalize(request.VisitType),
            Objective = PlannedVisitValidation.Trim(request.Objective),
            Notes = PlannedVisitValidation.Trim(request.Notes),
            BusinessUnit = PlannedVisitValidation.Trim(request.BusinessUnit),
            TerritoryNodeId = request.TerritoryNodeId,
            TerritoryModelId = request.TerritoryModelId,
            CampaignId = request.CampaignId,
            PlanStatus = status,
            Source = source,
            Content = journeyResult.ContentRef,
            Selection = BuildSelection(request, actor, now),
            Slot = new PlannedVisitScheduleSlot(), // motor-filled, born empty (D12/V26)
            CreatedAt = now,
            CreatedBy = actor
        };

        // Legacy planning guards (§21/L5-L6) — only an ACTIVE plan competes for a resource's day / a target's day-type.
        if (entity.IsActivePlan())
        {
            if (await FindOverlapAsync(tenantId, entity, cancellationToken) is { } overlapFailure)
            {
                return Fail(overlapFailure);
            }

            if (await FindDuplicateSameDayTypeAsync(tenantId, entity, cancellationToken) is { } dupFailure)
            {
                return Fail(dupFailure);
            }
        }

        // Derived provenance — read-only, stored not enforced (D5). Consent is recorded here; confirm is where it bites.
        entity.Frequency = await _frequencyProbe.ResolveAsync(entity, request.SegmentId, cancellationToken);
        entity.Consent = await _consentProbe.EvaluateAsync(entity, cancellationToken);
        entity.Availability = await _availabilityProbe.CaptureAsync(entity, cancellationToken);

        await _repository.InsertAsync(entity, cancellationToken);
        return Response<Guid>.Success(entity.Id, 201);
    }

    private static PlannedVisitSelectionProvenance BuildSelection(
        CreatePlannedVisitCommand request, string? actor, DateTimeOffset now) => new()
    {
        SegmentId = request.SegmentId,
        CampaignId = request.CampaignId,
        StrategyTemplateId = request.StrategyTemplateId,
        SelectionMode = PlannedVisitSelectionMode.Manual, // FU01 always manual (D11)
        DecidedAt = now,
        DecidedBy = actor
    };

    private async Task<PlannedVisitValidation.Failure?> FindOverlapAsync(
        Guid tenantId, PlannedVisitEntity plan, CancellationToken cancellationToken)
    {
        // Only rows that carry a time window compete (V23): a windowless plan is not a day-level block.
        if (plan.PlannedStartTime is null || plan.PlannedEndTime is null)
        {
            return null;
        }

        var sameDay = await _repository.ListByResourceAndDateAsync(
            tenantId, plan.Resource.ResourceId, plan.PlannedDate, cancellationToken);

        foreach (var other in sameDay)
        {
            if (other.Id == plan.Id || !other.IsActivePlan())
            {
                continue;
            }

            if (other.PlannedStartTime is null || other.PlannedEndTime is null)
            {
                continue;
            }

            if (WindowsOverlap(plan.PlannedStartTime, plan.PlannedEndTime, other.PlannedStartTime, other.PlannedEndTime))
            {
                return new PlannedVisitValidation.Failure(
                    $"This plan overlaps an active plan for the same resource on {plan.PlannedDate:yyyy-MM-dd} "
                    + $"(conflicting VisitCode '{other.VisitCode}').",
                    PlannedVisitErrorCodes.Overlap, 409);
            }
        }

        return null;
    }

    private async Task<PlannedVisitValidation.Failure?> FindDuplicateSameDayTypeAsync(
        Guid tenantId, PlannedVisitEntity plan, CancellationToken cancellationToken)
    {
        var sameDay = await _repository.ListByTargetAndDateAsync(
            tenantId, plan.TargetId, plan.PlannedDate, cancellationToken);

        var duplicate = sameDay.FirstOrDefault(other =>
            other.Id != plan.Id
            && other.IsActivePlan()
            && string.Equals(other.VisitType, plan.VisitType, StringComparison.Ordinal));

        return duplicate is null
            ? null
            : new PlannedVisitValidation.Failure(
                $"An active plan of the same visit type already exists for this target on {plan.PlannedDate:yyyy-MM-dd} "
                + $"(existing VisitCode '{duplicate.VisitCode}').",
                PlannedVisitErrorCodes.DuplicateSameDayType, 409);
    }

    /// <summary>Half-open overlap over "HH:mm" strings: touching windows (end == start) do not overlap.</summary>
    private static bool WindowsOverlap(string aStart, string aEnd, string bStart, string bEnd)
        => string.CompareOrdinal(aStart, bEnd) < 0 && string.CompareOrdinal(bStart, aEnd) < 0;

    private static Response<Guid> Fail(PlannedVisitValidation.Failure failure)
        => Response<Guid>.Fail(PlannedVisitValidation.ToErrors(failure), failure.StatusCode);
}
