using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney.Handlers;

using JourneyEntity = Diten.CrmService.Domain.Entities.ContentEngagementJourney;

/// <summary>
/// Shared FU05 journey/stage write-path helpers. TenantId is always the claim-resolved value. Vocabulary is validated
/// in-domain (structural, D-VOCAB=A). Nothing here deletes a journey or a stage. The freeze check lives in ONE place
/// (<see cref="EnsureNotFrozen"/>) so the stage handlers and the journey-update handler never diverge.
/// </summary>
internal static class ContentEngagementJourneyWrite
{
    /// <summary>V-J04/05/06 — subject required + non-archived; topic (if given) belongs to subject + non-archived;
    /// audience profile (if given) non-archived + same tenant. All violations are 400 (references, fail-closed). FU02
    /// aggregates are READ here and never mutated.</summary>
    public static async Task<string?> ValidateReferencesAsync(
        ISubjectRepository subjects, ITopicRepository topics, IAudienceProfileRepository profiles,
        Guid tenantId, Guid subjectId, Guid? topicId, Guid? audienceProfileId, CancellationToken ct)
    {
        var subject = await subjects.GetByIdAsync(tenantId, subjectId, ct);
        if (subject is null || subject.IsArchived())
        {
            return "SubjectId must reference a live, non-archived subject.";
        }

        if (topicId is { } tid && tid != Guid.Empty)
        {
            var topic = await topics.GetByIdAsync(tenantId, tid, ct);
            if (topic is null || topic.IsArchived())
            {
                return "TopicId must reference a live, non-archived topic.";
            }

            if (topic.SubjectId != subjectId)
            {
                return "TopicId must belong to the same SubjectId.";
            }
        }

        if (audienceProfileId is { } pid && pid != Guid.Empty)
        {
            var profile = await profiles.GetByIdAsync(tenantId, pid, ct);
            if (profile is null || profile.IsArchived())
            {
                return "AudienceProfileId must reference a live, non-archived audience profile.";
            }
        }

        return null;
    }

    /// <summary>V-S02 — a published journey's stage set is frozen; stage add/update/archive returns 409.</summary>
    public static string? EnsureNotFrozen(JourneyEntity journey)
        => journey.IsStageSetFrozen()
            ? "This journey version is published and its stage set is frozen. " +
              "Create a new version to change the stages."
            : null;

    public static List<ContentEngagementJourneyBranchCondition> MapBranchConditions(
        IReadOnlyList<ContentEngagementJourneyBranchConditionInput>? input)
        => (input ?? Array.Empty<ContentEngagementJourneyBranchConditionInput>())
            .Select(b => new ContentEngagementJourneyBranchCondition
            {
                ConditionCode = b.ConditionCode.Trim(),
                Description = ContentEngagementJourneyValidation.Trim(b.Description),
                TargetStageId = b.TargetStageId
            })
            .ToList();

    public static bool EffectiveWindowsOverlap(JourneyEntity a, JourneyEntity b)
    {
        var aStart = a.EffectiveFrom;
        var aEnd = a.EffectiveTo ?? DateTimeOffset.MaxValue;
        var bStart = b.EffectiveFrom;
        var bEnd = b.EffectiveTo ?? DateTimeOffset.MaxValue;
        return aStart <= bEnd && bStart <= aEnd;
    }
}

public sealed class CreateContentEngagementJourneyHandler
    : IRequestHandler<CreateContentEngagementJourneyCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IContentEngagementJourneyRepository _journeys;
    private readonly ISubjectRepository _subjects;
    private readonly ITopicRepository _topics;
    private readonly IAudienceProfileRepository _profiles;

    public CreateContentEngagementJourneyHandler(
        ITenantContext tenant, IActorContext actor, IContentEngagementJourneyRepository journeys,
        ISubjectRepository subjects, ITopicRepository topics, IAudienceProfileRepository profiles)
    {
        _tenant = tenant;
        _actor = actor;
        _journeys = journeys;
        _subjects = subjects;
        _topics = topics;
        _profiles = profiles;
    }

    public async Task<Response<Guid>> Handle(
        CreateContentEngagementJourneyCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var error = ContentEngagementJourneyValidation.ValidateJourneyCode(request.JourneyCode)
            ?? ContentEngagementJourneyValidation.ValidateJourneyName(request.JourneyName)
            ?? ContentEngagementJourneyValidation.ValidateObjective(request.Objective)
            ?? ContentEngagementJourneyValidation.ValidateDescription(request.Description)
            ?? ContentEngagementJourneyValidation.ValidateJourneyVersion(request.JourneyVersion)
            ?? ContentEngagementJourneyValidation.ValidateJourneyStatus(request.JourneyStatus)
            ?? ContentEngagementJourneyValidation.ValidateSource(request.Source)
            ?? KnowledgeValidation.ValidateEffectiveFrom(request.EffectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo)
            ?? KnowledgeValidation.ValidateRequiredSubject(request.SubjectId);
        if (error is not null)
        {
            return Response<Guid>.Fail(error, 400);
        }

        // A journey is never born published — publish is a separate endpoint (SoD).
        var status = ContentEngagementJourneyStatuses.Normalize(request.JourneyStatus);
        if (string.Equals(status, ContentEngagementJourneyStatuses.Published, StringComparison.Ordinal))
        {
            return Response<Guid>.Fail(
                "A journey cannot be created as published; use the publish endpoint.", 400);
        }

        var referenceError = await ContentEngagementJourneyWrite.ValidateReferencesAsync(
            _subjects, _topics, _profiles, tenantId, request.SubjectId, request.TopicId,
            request.AudienceProfileId, cancellationToken);
        if (referenceError is not null)
        {
            return Response<Guid>.Fail(referenceError, 400);
        }

        var code = request.JourneyCode.Trim();
        var version = request.JourneyVersion.Trim();
        var existing = await _journeys.ListByCodeAsync(tenantId, code, cancellationToken);
        if (existing.Any(j => !j.IsArchived()
                && string.Equals(j.JourneyVersion, version, StringComparison.OrdinalIgnoreCase)))
        {
            return Response<Guid>.Fail(
                $"A non-archived journey already uses JourneyCode '{code}' version '{version}'.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new JourneyEntity
        {
            TenantId = tenantId,
            JourneyCode = code,
            JourneyName = request.JourneyName.Trim(),
            Description = ContentEngagementJourneyValidation.Trim(request.Description),
            SubjectId = request.SubjectId,
            TopicId = request.TopicId,
            AudienceProfileId = request.AudienceProfileId,
            Objective = request.Objective.Trim(),
            LanguageCode = ContentEngagementJourneyValidation.Trim(request.LanguageCode),
            JourneyVersion = version,
            JourneyStatus = status,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Source = ContentEngagementJourneySources.Normalize(request.Source),
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _journeys.InsertAsync(entity, cancellationToken);
        return Response<Guid>.Success(entity.Id, 201);
    }
}

public sealed class UpdateContentEngagementJourneyHandler
    : IRequestHandler<UpdateContentEngagementJourneyCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IContentEngagementJourneyRepository _journeys;
    private readonly ISubjectRepository _subjects;
    private readonly ITopicRepository _topics;
    private readonly IAudienceProfileRepository _profiles;

    public UpdateContentEngagementJourneyHandler(
        ITenantContext tenant, IActorContext actor, IContentEngagementJourneyRepository journeys,
        ISubjectRepository subjects, ITopicRepository topics, IAudienceProfileRepository profiles)
    {
        _tenant = tenant;
        _actor = actor;
        _journeys = journeys;
        _subjects = subjects;
        _topics = topics;
        _profiles = profiles;
    }

    public async Task<Response<bool>> Handle(
        UpdateContentEngagementJourneyCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        // V-J16 — stages are managed only through the stage sub-routes; a `stages` array on Update is rejected outright.
        if (request.StagesProvided)
        {
            return Response<bool>.Fail(
                "Stages cannot be written through the journey update; use the stage sub-routes (V-J16).", 400);
        }

        var journey = await _journeys.GetByIdAsync(tenantId, request.JourneyId, cancellationToken);
        if (journey is null)
        {
            return Response<bool>.Fail("Content engagement journey not found.", 404);
        }

        if (journey.IsArchived())
        {
            return Response<bool>.Fail("An archived journey cannot be updated.", 409);
        }

        if (request.ExpectedVersion is { } ev && ev != journey.Version)
        {
            return Response<bool>.Fail("The journey was modified by another writer; reload and retry.", 409);
        }

        var newStatus = ContentEngagementJourneyStatuses.Normalize(request.JourneyStatus ?? journey.JourneyStatus);

        // V-J12 — publish is a separate endpoint; Update may not transition to published.
        if (string.Equals(newStatus, ContentEngagementJourneyStatuses.Published, StringComparison.Ordinal)
            && !journey.IsPublished())
        {
            return Response<bool>.Fail("Use the publish endpoint to publish a journey (V-J12).", 400);
        }

        var scalarError = ContentEngagementJourneyValidation.ValidateJourneyName(request.JourneyName)
            ?? ContentEngagementJourneyValidation.ValidateObjective(request.Objective)
            ?? ContentEngagementJourneyValidation.ValidateDescription(request.Description)
            ?? ContentEngagementJourneyValidation.ValidateJourneyVersion(request.JourneyVersion)
            ?? ContentEngagementJourneyValidation.ValidateJourneyStatus(request.JourneyStatus)
            ?? ContentEngagementJourneyValidation.ValidateSource(request.Source)
            ?? KnowledgeValidation.ValidateEffectiveFrom(request.EffectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo)
            ?? KnowledgeValidation.ValidateRequiredSubject(request.SubjectId);
        if (scalarError is not null)
        {
            return Response<bool>.Fail(scalarError, 400);
        }

        // V-J13 — a published version is frozen: only EffectiveTo and a lifecycle move (inactive/archived) may change.
        if (journey.IsStageSetFrozen())
        {
            var onlyAllowedChanges =
                journey.JourneyName == request.JourneyName.Trim()
                && journey.SubjectId == request.SubjectId
                && journey.TopicId == request.TopicId
                && journey.AudienceProfileId == request.AudienceProfileId
                && journey.Objective == request.Objective.Trim()
                && string.Equals(
                    journey.LanguageCode, ContentEngagementJourneyValidation.Trim(request.LanguageCode))
                && string.Equals(
                    journey.JourneyVersion, request.JourneyVersion.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(journey.Description, ContentEngagementJourneyValidation.Trim(request.Description))
                && string.Equals(
                    journey.Source, ContentEngagementJourneySources.Normalize(request.Source), StringComparison.Ordinal)
                && journey.EffectiveFrom == request.EffectiveFrom;

            var statusAllowed = string.Equals(newStatus, journey.JourneyStatus, StringComparison.Ordinal)
                || newStatus is ContentEngagementJourneyStatuses.Inactive
                    or ContentEngagementJourneyStatuses.Archived;

            if (!onlyAllowedChanges || !statusAllowed)
            {
                return Response<bool>.Fail(
                    "A published journey version is frozen; only EffectiveTo and a lifecycle change are allowed. " +
                    "Create a new version to change it (V-J13).", 409);
            }
        }
        else
        {
            var referenceError = await ContentEngagementJourneyWrite.ValidateReferencesAsync(
                _subjects, _topics, _profiles, tenantId, request.SubjectId, request.TopicId,
                request.AudienceProfileId, cancellationToken);
            if (referenceError is not null)
            {
                return Response<bool>.Fail(referenceError, 400);
            }
        }

        journey.JourneyName = request.JourneyName.Trim();
        journey.Description = ContentEngagementJourneyValidation.Trim(request.Description);
        journey.SubjectId = request.SubjectId;
        journey.TopicId = request.TopicId;
        journey.AudienceProfileId = request.AudienceProfileId;
        journey.Objective = request.Objective.Trim();
        journey.LanguageCode = ContentEngagementJourneyValidation.Trim(request.LanguageCode);
        journey.JourneyVersion = request.JourneyVersion.Trim();
        journey.JourneyStatus = newStatus;
        journey.EffectiveFrom = request.EffectiveFrom;
        journey.EffectiveTo = request.EffectiveTo;
        journey.Source = ContentEngagementJourneySources.Normalize(request.Source);
        journey.UpdatedAt = DateTimeOffset.UtcNow;
        journey.UpdatedBy = _actor.ActorName;

        var ok = await _journeys.ReplaceAsync(journey, journey.Version, cancellationToken);
        return ok
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The journey was modified by another writer; reload and retry.", 409);
    }
}

public sealed class PublishContentEngagementJourneyHandler
    : IRequestHandler<PublishContentEngagementJourneyCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IContentEngagementJourneyRepository _journeys;

    public PublishContentEngagementJourneyHandler(
        ITenantContext tenant, IActorContext actor, IContentEngagementJourneyRepository journeys)
    {
        _tenant = tenant;
        _actor = actor;
        _journeys = journeys;
    }

    public async Task<Response<bool>> Handle(
        PublishContentEngagementJourneyCommand request, CancellationToken cancellationToken)
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
            return Response<bool>.Fail("An archived journey cannot be published.", 409);
        }

        if (request.ExpectedVersion is { } ev && ev != journey.Version)
        {
            return Response<bool>.Fail("The journey was modified by another writer; reload and retry.", 409);
        }

        if (journey.IsPublished())
        {
            return Response<bool>.Success(true); // idempotent — already published + frozen
        }

        // V-J11 — a published journey must carry at least one active, required stage.
        if (!journey.ActiveStages().Any(s => s.IsRequired))
        {
            return Response<bool>.Fail(
                "A journey can only be published with at least one active, required stage (V-J11).", 400);
        }

        // V-J10 — no second published version of the same (JourneyCode, LanguageCode) may overlap in effective window.
        var siblings = await _journeys.ListByCodeAsync(tenantId, journey.JourneyCode, cancellationToken);
        var overlap = siblings.Any(other =>
            other.Id != journey.Id
            && !other.IsArchived()
            && other.IsPublished()
            && string.Equals(other.LanguageCode, journey.LanguageCode, StringComparison.OrdinalIgnoreCase)
            && ContentEngagementJourneyWrite.EffectiveWindowsOverlap(other, journey));
        if (overlap)
        {
            return Response<bool>.Fail(
                "Another published version of this JourneyCode already overlaps this effective window (V-J10).", 409);
        }

        var now = DateTimeOffset.UtcNow;
        journey.JourneyStatus = ContentEngagementJourneyStatuses.Published;
        journey.StageSetFrozenAt = now;
        journey.PublishedAt = now;
        journey.PublishedBy = _actor.ActorName;
        journey.UpdatedAt = now;
        journey.UpdatedBy = _actor.ActorName;

        var ok = await _journeys.ReplaceAsync(journey, journey.Version, cancellationToken);
        return ok
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The journey was modified by another writer; reload and retry.", 409);
    }
}

public sealed class CreateContentEngagementJourneyVersionHandler
    : IRequestHandler<CreateContentEngagementJourneyVersionCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IContentEngagementJourneyRepository _journeys;

    public CreateContentEngagementJourneyVersionHandler(
        ITenantContext tenant, IActorContext actor, IContentEngagementJourneyRepository journeys)
    {
        _tenant = tenant;
        _actor = actor;
        _journeys = journeys;
    }

    public async Task<Response<Guid>> Handle(
        CreateContentEngagementJourneyVersionCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var source = await _journeys.GetByIdAsync(tenantId, request.JourneyId, cancellationToken);
        if (source is null)
        {
            return Response<Guid>.Fail("Content engagement journey not found.", 404);
        }

        // V-J14 — only a published version carries a frozen stage set worth cloning.
        if (!source.IsPublished())
        {
            return Response<Guid>.Fail(
                "Only a published journey version can be used to create a new version (V-J14).", 400);
        }

        var siblings = await _journeys.ListByCodeAsync(tenantId, source.JourneyCode, cancellationToken);
        var takenVersions = siblings
            .Where(j => !j.IsArchived())
            .Select(j => j.JourneyVersion)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string newVersion;
        if (!string.IsNullOrWhiteSpace(request.NewJourneyVersion))
        {
            newVersion = request.NewJourneyVersion.Trim();
            if (takenVersions.Contains(newVersion))
            {
                return Response<Guid>.Fail(
                    $"A non-archived journey already uses JourneyCode '{source.JourneyCode}' " +
                    $"version '{newVersion}'.", 409);
            }
        }
        else
        {
            newVersion = NextFreeVersion(source.JourneyVersion, takenVersions);
        }

        // AC-FREEZE-2: copy the stages with NEW StageIds and REMAP every internal reference (FallbackStageId and each
        // BranchConditions[].TargetStageId) through the old→new id map. Skipping the remap would leave the clone
        // silently pointing at the previous version's stages (the FU04/D5 trap).
        var idMap = source.Stages.ToDictionary(s => s.StageId, _ => Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;
        var copiedStages = source.Stages.Select(s => new ContentEngagementJourneyStage
        {
            StageId = idMap[s.StageId],
            StageOrder = s.StageOrder,
            StageCode = s.StageCode,
            StageName = s.StageName,
            StageObjective = s.StageObjective,
            StageType = s.StageType,
            RecommendedKnowledgePathId = s.RecommendedKnowledgePathId,
            PathCode = s.PathCode,
            PathVersionPinPolicy = s.PathVersionPinPolicy,
            IsRequired = s.IsRequired,
            Repeatable = s.Repeatable,
            MinVisitNumber = s.MinVisitNumber,
            MaxVisitNumber = s.MaxVisitNumber,
            AdvancementRule = s.AdvancementRule,
            FallbackStageId = s.FallbackStageId is { } f && idMap.TryGetValue(f, out var nf) ? nf : null,
            BranchConditions = s.BranchConditions.Select(b => new ContentEngagementJourneyBranchCondition
            {
                ConditionCode = b.ConditionCode,
                Description = b.Description,
                TargetStageId = b.TargetStageId is { } t && idMap.TryGetValue(t, out var nt) ? nt : b.TargetStageId
            }).ToList(),
            Notes = s.Notes,
            StageStatus = s.StageStatus,
            ArchivedAt = s.ArchivedAt,
            ArchivedBy = s.ArchivedBy,
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        }).ToList();

        var clone = new JourneyEntity
        {
            TenantId = tenantId,
            JourneyCode = source.JourneyCode,
            JourneyName = source.JourneyName,
            Description = source.Description,
            SubjectId = source.SubjectId,
            TopicId = source.TopicId,
            AudienceProfileId = source.AudienceProfileId,
            Objective = source.Objective,
            LanguageCode = source.LanguageCode,
            JourneyVersion = newVersion,
            JourneyStatus = ContentEngagementJourneyStatuses.Draft,
            EffectiveFrom = source.EffectiveFrom,
            EffectiveTo = source.EffectiveTo,
            Source = source.Source,
            Stages = copiedStages,
            SupersedesJourneyId = source.Id,
            StageSetFrozenAt = null,
            PublishedAt = null,
            PublishedBy = null,
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _journeys.InsertAsync(clone, cancellationToken);
        return Response<Guid>.Success(clone.Id, 201);
    }

    private static string NextFreeVersion(string current, IReadOnlySet<string> taken)
    {
        if (double.TryParse(current, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            var next = Math.Floor(value) + 1;
            var candidate = next.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
            while (taken.Contains(candidate))
            {
                next += 1;
                candidate = next.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
            }

            return candidate;
        }

        var suffix = 2;
        var built = $"{current}.{suffix}";
        while (taken.Contains(built))
        {
            suffix++;
            built = $"{current}.{suffix}";
        }

        return built;
    }
}

public sealed class ArchiveContentEngagementJourneyHandler
    : IRequestHandler<ArchiveContentEngagementJourneyCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IContentEngagementJourneyRepository _journeys;

    public ArchiveContentEngagementJourneyHandler(
        ITenantContext tenant, IActorContext actor, IContentEngagementJourneyRepository journeys)
    {
        _tenant = tenant;
        _actor = actor;
        _journeys = journeys;
    }

    public async Task<Response<bool>> Handle(
        ArchiveContentEngagementJourneyCommand request, CancellationToken cancellationToken)
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
            return Response<bool>.Success(true); // idempotent
        }

        if (request.ExpectedVersion is { } ev && ev != journey.Version)
        {
            return Response<bool>.Fail("The journey was modified by another writer; reload and retry.", 409);
        }

        // V-S20 — archiving the journey does NOT touch the embedded stages: they stay in the SAME document and are
        // treated as archived through the parent (no cascade write, no element removal).
        var now = DateTimeOffset.UtcNow;
        journey.JourneyStatus = ContentEngagementJourneyStatuses.Archived;
        journey.ArchivedAt = now;
        journey.ArchivedBy = _actor.ActorName;
        journey.UpdatedAt = now;
        journey.UpdatedBy = _actor.ActorName;

        var ok = await _journeys.ReplaceAsync(journey, journey.Version, cancellationToken);
        return ok
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The journey was modified by another writer; reload and retry.", 409);
    }
}
