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
/// Edits a plan. The code and the lifecycle status are NOT inputs (the code is never renamed; the status moves only
/// through confirm / cancel / archive). An archived plan accepts nothing (409). A past PlannedDate is refused unless the
/// plan is still <c>draft</c> (V7). Optimistic concurrency is enforced against the expected Version (409 on mismatch).
/// <para>Every re-derivable provenance block (frequency / consent / availability) is recomputed on the new shape so a
/// stored snapshot never silently diverges from the plan it describes. The content-position ref is rebuilt from 26/27
/// (the single source of truth, D10) with the same published/effective + stage-in-journey checks as create.</para>
/// </summary>
public sealed class UpdatePlannedVisitHandler : IRequestHandler<UpdatePlannedVisitCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IPlannedVisitRepository _repository;
    private readonly PlannedVisitWriteGuards _guards;
    private readonly PlannedVisitJourneyProbe _journeyProbe;
    private readonly PlannedVisitFrequencyProbe _frequencyProbe;
    private readonly PlannedVisitConsentProbe _consentProbe;
    private readonly PlannedVisitAvailabilityProbe _availabilityProbe;

    public UpdatePlannedVisitHandler(
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

    public async Task<Response<bool>> Handle(UpdatePlannedVisitCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var plan = await _repository.GetByIdAsync(tenantId, request.PlannedVisitId, cancellationToken);
        if (plan is null)
        {
            return Response<bool>.Fail("Planned visit not found.", 404);
        }

        if (plan.IsArchived())
        {
            return Fail(new PlannedVisitValidation.Failure(
                "An archived plan cannot be modified.", PlannedVisitErrorCodes.Archived, 409));
        }

        var shapeFailure = PlannedVisitValidation.ValidateShape(
            null, request.TargetType, request.TargetId, request.ResourceId, request.ResourceType,
            request.PlannedStartTime, request.PlannedEndTime, request.PlannedDurationMinutes,
            request.VisitPurpose, request.VisitType, request.Objective, request.Notes, validateCode: false);
        if (shapeFailure is not null)
        {
            return Fail(shapeFailure);
        }

        var plannedDate = PlannedVisitValidation.ParseDate(request.PlannedDate);
        if (plannedDate is not { } date)
        {
            return Fail(new PlannedVisitValidation.Failure("PlannedDate is required.", PlannedVisitErrorCodes.DateRequired));
        }

        // A past date is allowed only while the plan is still draft (V7/AC-TIME-3).
        if (date < DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime) && !plan.IsDraft())
        {
            return Fail(new PlannedVisitValidation.Failure(
                "PlannedDate cannot be in the past for a non-draft plan.", PlannedVisitErrorCodes.DateInPast));
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

        var expectedVersion = request.ExpectedVersion ?? plan.Version;
        if (expectedVersion != plan.Version)
        {
            return ConcurrencyFail();
        }

        var target = targetResult.Target;
        var now = DateTimeOffset.UtcNow;
        var actor = _actor.ActorName;

        plan.TargetType = PlannedVisitTargetType.Normalize(request.TargetType);
        plan.TargetId = request.TargetId;
        plan.AccountId = target.AccountId;
        plan.ContactId = target.ContactId;
        plan.AccountContactLinkId = target.AccountContactLinkId;
        plan.PlannedDate = date;
        plan.PlannedStartTime = PlannedVisitValidation.Trim(request.PlannedStartTime);
        plan.PlannedEndTime = PlannedVisitValidation.Trim(request.PlannedEndTime);
        plan.PlannedDurationMinutes = request.PlannedDurationMinutes;
        plan.Resource = new PlannedVisitResourceRef
        {
            ResourceId = request.ResourceId.Trim(),
            ResourceType = PlannedVisitResourceTypes.Normalize(request.ResourceType),
            DisplayName = PlannedVisitValidation.Trim(request.ResourceDisplayName)
        };
        plan.PositionCode = PlannedVisitValidation.Trim(request.PositionCode);
        plan.PositionId = request.PositionId;
        plan.VisitPurpose = PlannedVisitPurpose.Normalize(request.VisitPurpose);
        plan.VisitType = PlannedVisitType.Normalize(request.VisitType);
        plan.Objective = PlannedVisitValidation.Trim(request.Objective);
        plan.Notes = PlannedVisitValidation.Trim(request.Notes);
        plan.BusinessUnit = PlannedVisitValidation.Trim(request.BusinessUnit);
        plan.TerritoryNodeId = request.TerritoryNodeId;
        plan.TerritoryModelId = request.TerritoryModelId;
        plan.CampaignId = request.CampaignId;
        plan.Content = journeyResult.ContentRef;
        plan.Selection = new PlannedVisitSelectionProvenance
        {
            SegmentId = request.SegmentId,
            CampaignId = request.CampaignId,
            StrategyTemplateId = request.StrategyTemplateId,
            SelectionMode = PlannedVisitSelectionMode.Manual,
            DecidedAt = plan.Selection?.DecidedAt ?? now,
            DecidedBy = plan.Selection?.DecidedBy ?? actor
        };

        // Legacy planning guards (§21/L5-L6) — only when the plan is ACTIVE; self is excluded by id.
        if (plan.IsActivePlan())
        {
            if (await FindOverlapAsync(tenantId, plan, cancellationToken) is { } overlapFailure)
            {
                return Fail(overlapFailure);
            }

            if (await FindDuplicateSameDayTypeAsync(tenantId, plan, cancellationToken) is { } dupFailure)
            {
                return Fail(dupFailure);
            }
        }

        // Recompute the re-derivable provenance on the new shape (D5).
        plan.Frequency = await _frequencyProbe.ResolveAsync(plan, request.SegmentId, cancellationToken);
        plan.Consent = await _consentProbe.EvaluateAsync(plan, cancellationToken);
        plan.Availability = await _availabilityProbe.CaptureAsync(plan, cancellationToken);

        plan.UpdatedAt = now;
        plan.UpdatedBy = actor;

        var replaced = await _repository.ReplaceAsync(plan, expectedVersion, cancellationToken);
        return replaced ? Response<bool>.Success(true) : ConcurrencyFail();
    }

    private async Task<PlannedVisitValidation.Failure?> FindOverlapAsync(
        Guid tenantId, PlannedVisitEntity plan, CancellationToken cancellationToken)
    {
        if (plan.PlannedStartTime is null || plan.PlannedEndTime is null)
        {
            return null;
        }

        var sameDay = await _repository.ListByResourceAndDateAsync(
            tenantId, plan.Resource.ResourceId, plan.PlannedDate, cancellationToken);

        foreach (var other in sameDay)
        {
            if (other.Id == plan.Id || !other.IsActivePlan()
                || other.PlannedStartTime is null || other.PlannedEndTime is null)
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

    private static bool WindowsOverlap(string aStart, string aEnd, string bStart, string bEnd)
        => string.CompareOrdinal(aStart, bEnd) < 0 && string.CompareOrdinal(bStart, aEnd) < 0;

    private static Response<bool> Fail(PlannedVisitValidation.Failure failure)
        => Response<bool>.Fail(PlannedVisitValidation.ToErrors(failure), failure.StatusCode);

    private static Response<bool> ConcurrencyFail()
        => Response<bool>.Fail(
            new[] { "The plan changed since it was loaded. Reload and try again.", PlannedVisitErrorCodes.ConcurrencyConflict },
            409);
}
