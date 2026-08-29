using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney.Handlers;

/// <summary>MOD-0162 FU05 embedded-stage write handlers (S2). Each mutates the SAME journey document and rides the
/// journey's optimistic <see cref="EntityBase.Version"/> token — a stage write bumps the journey Version. In-array
/// StageOrder/StageCode uniqueness has no DB index, so the handler is the only defence (§4.5). The FU04 KnowledgePath
/// is READ for the binding guard and never mutated.</summary>
public sealed class AddContentEngagementJourneyStageHandler
    : IRequestHandler<AddContentEngagementJourneyStageCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IContentEngagementJourneyRepository _journeys;
    private readonly ContentEngagementJourneyPathResolver _pathResolver;

    public AddContentEngagementJourneyStageHandler(
        ITenantContext tenant, IActorContext actor, IContentEngagementJourneyRepository journeys,
        ContentEngagementJourneyPathResolver pathResolver)
    {
        _tenant = tenant;
        _actor = actor;
        _journeys = journeys;
        _pathResolver = pathResolver;
    }

    public async Task<Response<Guid>> Handle(
        AddContentEngagementJourneyStageCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var journey = await _journeys.GetByIdAsync(tenantId, request.JourneyId, cancellationToken);
        if (journey is null)
        {
            return Response<Guid>.Fail("Content engagement journey not found.", 404);
        }

        if (journey.IsArchived())
        {
            return Response<Guid>.Fail("An archived journey cannot be modified.", 409);
        }

        if (ContentEngagementJourneyWrite.EnsureNotFrozen(journey) is { } frozen)
        {
            return Response<Guid>.Fail(frozen, 409);
        }

        if (request.ExpectedVersion is { } ev && ev != journey.Version)
        {
            return Response<Guid>.Fail("The journey was modified by another writer; reload and retry.", 409);
        }

        // V-S18 — document growth guard.
        if (ContentEngagementJourneyValidation.ValidateStageLimit(journey) is { } limit)
        {
            return Response<Guid>.Fail(limit, 400);
        }

        var scalarError = ContentEngagementJourneyValidation.ValidateStageCode(request.StageCode)
            ?? ContentEngagementJourneyValidation.ValidateStageName(request.StageName)
            ?? ContentEngagementJourneyValidation.ValidateStageObjective(request.StageObjective)
            ?? ContentEngagementJourneyValidation.ValidateStageType(request.StageType)
            ?? ContentEngagementJourneyValidation.ValidateAdvancementRule(request.AdvancementRule)
            ?? ContentEngagementJourneyValidation.ValidatePathPin(request.PathVersionPinPolicy)
            ?? ContentEngagementJourneyValidation.ValidateNotes(request.Notes)
            ?? ContentEngagementJourneyValidation.ValidateVisitRange(request.MinVisitNumber, request.MaxVisitNumber)
            ?? ContentEngagementJourneyValidation.ValidateBranchShape(request.BranchConditions);
        if (scalarError is not null)
        {
            return Response<Guid>.Fail(scalarError, 400);
        }

        // V-S03/S04 — unique among active stages (handler is the only defence).
        if (ContentEngagementJourneyValidation.ValidateStageUniqueness(
                journey, request.StageOrder, request.StageCode, null) is { } dup)
        {
            return Response<Guid>.Fail(dup, 409);
        }

        // V-S05/S06 — the stage binds to a published + effective KnowledgePath (read-only FU04 access).
        var path = await _pathResolver.GetPathAsync(
            tenantId, request.RecommendedKnowledgePathId, cancellationToken);
        if (ContentEngagementJourneyPathResolver.ValidateBindablePath(path, DateTimeOffset.UtcNow) is { } pathError)
        {
            return Response<Guid>.Fail(pathError, 400);
        }

        var newStageId = Guid.NewGuid();

        // V-S10 — fallback must be another active stage of the same journey (backwards is allowed, never evaluated).
        if (ContentEngagementJourneyValidation.ValidateFallback(
                journey, request.FallbackStageId, newStageId) is { } fallbackError)
        {
            return Response<Guid>.Fail(fallbackError, 400);
        }

        // V-S15 — branch targets must reference a stage in the same journey (this new stage included).
        var stageIds = journey.Stages.Select(s => s.StageId).Append(newStageId).ToHashSet();
        if (ContentEngagementJourneyValidation.ValidateBranchTargets(
                request.BranchConditions, stageIds) is { } branchError)
        {
            return Response<Guid>.Fail(branchError, 400);
        }

        var now = DateTimeOffset.UtcNow;
        var stage = new ContentEngagementJourneyStage
        {
            StageId = newStageId,
            StageOrder = request.StageOrder,
            StageCode = request.StageCode.Trim(),
            StageName = request.StageName.Trim(),
            StageObjective = request.StageObjective.Trim(),
            StageType = string.IsNullOrWhiteSpace(request.StageType)
                ? null
                : ContentEngagementJourneyStageTypes.Normalize(request.StageType),
            RecommendedKnowledgePathId = request.RecommendedKnowledgePathId,
            PathCode = path!.PathCode,
            PathVersionPinPolicy = ContentEngagementJourneyPathPin.Normalize(request.PathVersionPinPolicy),
            IsRequired = request.IsRequired,
            Repeatable = request.Repeatable,
            MinVisitNumber = request.MinVisitNumber,
            MaxVisitNumber = request.MaxVisitNumber,
            AdvancementRule = string.IsNullOrWhiteSpace(request.AdvancementRule)
                ? null
                : ContentEngagementJourneyAdvancementRules.Normalize(request.AdvancementRule),
            FallbackStageId = request.FallbackStageId,
            BranchConditions = ContentEngagementJourneyWrite.MapBranchConditions(request.BranchConditions),
            Notes = ContentEngagementJourneyValidation.Trim(request.Notes),
            StageStatus = ContentEngagementJourneyStageStatuses.Active,
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        journey.Stages.Add(stage);
        journey.UpdatedAt = now;
        journey.UpdatedBy = _actor.ActorName;

        var ok = await _journeys.ReplaceAsync(journey, journey.Version, cancellationToken);
        return ok
            ? Response<Guid>.Success(newStageId, 201)
            : Response<Guid>.Fail("The journey was modified by another writer; reload and retry.", 409);
    }
}

public sealed class UpdateContentEngagementJourneyStageHandler
    : IRequestHandler<UpdateContentEngagementJourneyStageCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IContentEngagementJourneyRepository _journeys;
    private readonly ContentEngagementJourneyPathResolver _pathResolver;

    public UpdateContentEngagementJourneyStageHandler(
        ITenantContext tenant, IActorContext actor, IContentEngagementJourneyRepository journeys,
        ContentEngagementJourneyPathResolver pathResolver)
    {
        _tenant = tenant;
        _actor = actor;
        _journeys = journeys;
        _pathResolver = pathResolver;
    }

    public async Task<Response<bool>> Handle(
        UpdateContentEngagementJourneyStageCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var journey = await _journeys.GetByIdAsync(tenantId, request.JourneyId, cancellationToken);
        if (journey is null)
        {
            return Response<bool>.Fail("Content engagement journey not found.", 404);
        }

        if (journey.IsArchived())
        {
            return Response<bool>.Fail("An archived journey cannot be modified.", 409);
        }

        if (ContentEngagementJourneyWrite.EnsureNotFrozen(journey) is { } frozen)
        {
            return Response<bool>.Fail(frozen, 409);
        }

        if (request.ExpectedVersion is { } ev && ev != journey.Version)
        {
            return Response<bool>.Fail("The journey was modified by another writer; reload and retry.", 409);
        }

        var stage = journey.Stages.FirstOrDefault(s => s.StageId == request.StageId);
        if (stage is null)
        {
            return Response<bool>.Fail("Stage not found in this journey.", 404);
        }

        if (stage.IsArchived())
        {
            return Response<bool>.Fail("An archived stage cannot be updated.", 409);
        }

        var scalarError = ContentEngagementJourneyValidation.ValidateStageCode(request.StageCode)
            ?? ContentEngagementJourneyValidation.ValidateStageName(request.StageName)
            ?? ContentEngagementJourneyValidation.ValidateStageObjective(request.StageObjective)
            ?? ContentEngagementJourneyValidation.ValidateStageType(request.StageType)
            ?? ContentEngagementJourneyValidation.ValidateAdvancementRule(request.AdvancementRule)
            ?? ContentEngagementJourneyValidation.ValidatePathPin(request.PathVersionPinPolicy)
            ?? ContentEngagementJourneyValidation.ValidateNotes(request.Notes)
            ?? ContentEngagementJourneyValidation.ValidateVisitRange(request.MinVisitNumber, request.MaxVisitNumber)
            ?? ContentEngagementJourneyValidation.ValidateBranchShape(request.BranchConditions);
        if (scalarError is not null)
        {
            return Response<bool>.Fail(scalarError, 400);
        }

        if (ContentEngagementJourneyValidation.ValidateStageUniqueness(
                journey, request.StageOrder, request.StageCode, request.StageId) is { } dup)
        {
            return Response<bool>.Fail(dup, 409);
        }

        // V-S07 dirty-check — re-validate the path binding ONLY when RecommendedKnowledgePathId actually changed. An
        // untouched binding is never re-validated, so a path that later left its window does not break an edit of an
        // unrelated field (FU03 V22 / FU04 V-S07 precedent).
        var pathCode = stage.PathCode;
        if (request.RecommendedKnowledgePathId != stage.RecommendedKnowledgePathId)
        {
            var path = await _pathResolver.GetPathAsync(
                tenantId, request.RecommendedKnowledgePathId, cancellationToken);
            if (ContentEngagementJourneyPathResolver.ValidateBindablePath(path, DateTimeOffset.UtcNow) is { } pathError)
            {
                return Response<bool>.Fail(pathError, 400);
            }

            pathCode = path!.PathCode;
        }

        if (ContentEngagementJourneyValidation.ValidateFallback(
                journey, request.FallbackStageId, request.StageId) is { } fallbackError)
        {
            return Response<bool>.Fail(fallbackError, 400);
        }

        var stageIds = journey.Stages.Select(s => s.StageId).ToHashSet();
        if (ContentEngagementJourneyValidation.ValidateBranchTargets(
                request.BranchConditions, stageIds) is { } branchError)
        {
            return Response<bool>.Fail(branchError, 400);
        }

        var now = DateTimeOffset.UtcNow;
        stage.StageOrder = request.StageOrder;
        stage.StageCode = request.StageCode.Trim();
        stage.StageName = request.StageName.Trim();
        stage.StageObjective = request.StageObjective.Trim();
        stage.StageType = string.IsNullOrWhiteSpace(request.StageType)
            ? null
            : ContentEngagementJourneyStageTypes.Normalize(request.StageType);
        stage.RecommendedKnowledgePathId = request.RecommendedKnowledgePathId;
        stage.PathCode = pathCode;
        stage.PathVersionPinPolicy = ContentEngagementJourneyPathPin.Normalize(request.PathVersionPinPolicy);
        stage.IsRequired = request.IsRequired;
        stage.Repeatable = request.Repeatable;
        stage.MinVisitNumber = request.MinVisitNumber;
        stage.MaxVisitNumber = request.MaxVisitNumber;
        stage.AdvancementRule = string.IsNullOrWhiteSpace(request.AdvancementRule)
            ? null
            : ContentEngagementJourneyAdvancementRules.Normalize(request.AdvancementRule);
        stage.FallbackStageId = request.FallbackStageId;
        stage.BranchConditions = ContentEngagementJourneyWrite.MapBranchConditions(request.BranchConditions);
        stage.Notes = ContentEngagementJourneyValidation.Trim(request.Notes);
        stage.UpdatedAt = now;
        stage.UpdatedBy = _actor.ActorName;
        journey.UpdatedAt = now;
        journey.UpdatedBy = _actor.ActorName;

        var ok = await _journeys.ReplaceAsync(journey, journey.Version, cancellationToken);
        return ok
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The journey was modified by another writer; reload and retry.", 409);
    }
}

public sealed class ArchiveContentEngagementJourneyStageHandler
    : IRequestHandler<ArchiveContentEngagementJourneyStageCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IContentEngagementJourneyRepository _journeys;

    public ArchiveContentEngagementJourneyStageHandler(
        ITenantContext tenant, IActorContext actor, IContentEngagementJourneyRepository journeys)
    {
        _tenant = tenant;
        _actor = actor;
        _journeys = journeys;
    }

    public async Task<Response<bool>> Handle(
        ArchiveContentEngagementJourneyStageCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var journey = await _journeys.GetByIdAsync(tenantId, request.JourneyId, cancellationToken);
        if (journey is null)
        {
            return Response<bool>.Fail("Content engagement journey not found.", 404);
        }

        if (journey.IsArchived())
        {
            return Response<bool>.Fail("An archived journey cannot be modified.", 409);
        }

        if (ContentEngagementJourneyWrite.EnsureNotFrozen(journey) is { } frozen)
        {
            return Response<bool>.Fail(frozen, 409);
        }

        if (request.ExpectedVersion is { } ev && ev != journey.Version)
        {
            return Response<bool>.Fail("The journey was modified by another writer; reload and retry.", 409);
        }

        var stage = journey.Stages.FirstOrDefault(s => s.StageId == request.StageId);
        if (stage is null)
        {
            return Response<bool>.Fail("Stage not found in this journey.", 404);
        }

        if (stage.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        // V-S17 — an active stage still referenced by another active stage (fallback or branch target) cannot be
        // archived (dangling reference guard).
        if (ContentEngagementJourneyValidation.ValidateNoDanglingReference(journey, stage.StageId) is { } dangling)
        {
            return Response<bool>.Fail(dangling, 409);
        }

        // The element is NEVER removed from the array: it is flagged archived and stays in the same document.
        var now = DateTimeOffset.UtcNow;
        stage.StageStatus = ContentEngagementJourneyStageStatuses.Archived;
        stage.ArchivedAt = now;
        stage.ArchivedBy = _actor.ActorName;
        stage.UpdatedAt = now;
        stage.UpdatedBy = _actor.ActorName;
        journey.UpdatedAt = now;
        journey.UpdatedBy = _actor.ActorName;

        var ok = await _journeys.ReplaceAsync(journey, journey.Version, cancellationToken);
        return ok
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The journey was modified by another writer; reload and retry.", 409);
    }
}
