using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.Path.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Path.Handlers;

/// <summary>
/// Shared FU04 path/step write-path helpers. TenantId is always the claim-resolved value. Vocabulary is validated
/// in-domain (structural). Nothing here deletes a path or a step. The freeze check lives in ONE place
/// (<see cref="EnsureNotFrozen"/>) so the step handlers and the path-update handler never diverge.
/// </summary>
internal static class KnowledgePathWrite
{
    /// <summary>V-P04/05/06 — subject required + non-archived; topic (if given) belongs to subject + non-archived;
    /// audience profile (if given) non-archived + same tenant. All violations are 400 (references, fail-closed).</summary>
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

    /// <summary>V-S05/06/12 — the step content must exist, be published + effective for the path window and non-archived;
    /// when CompletionRule = assessment-passed the content must be a quiz (D6=A). Returns the content (for ContentCode)
    /// or an error+code.</summary>
    public static async Task<(KnowledgeContent? Content, string? Error, int StatusCode)> ValidateStepContentAsync(
        IKnowledgeContentRepository contents, Guid tenantId, KnowledgePath path, Guid contentId,
        string? completionRule, CancellationToken ct)
    {
        if (contentId == Guid.Empty)
        {
            return (null, "ContentId is required and cannot be empty.", 400);
        }

        var content = await contents.GetByIdAsync(tenantId, contentId, ct);
        if (content is null)
        {
            return (null, "ContentId does not reference existing content in this tenant.", 400);
        }

        if (content.IsArchived())
        {
            return (null, "A step cannot reference archived content.", 400);
        }

        if (!string.Equals(content.ContentStatus, KnowledgeContentStatuses.Published, StringComparison.OrdinalIgnoreCase))
        {
            return (null, "A step may only reference published content.", 400);
        }

        if (content.EffectiveTo is { } to && to < path.EffectiveFrom)
        {
            return (null, "The referenced content is not effective for the path's effective window.", 400);
        }

        if (string.Equals(
                KnowledgePathCompletionRules.Normalize(completionRule),
                KnowledgePathCompletionRules.AssessmentPassed, StringComparison.Ordinal)
            && !string.Equals(content.ContentType, KnowledgeContentTypes.Quiz, StringComparison.OrdinalIgnoreCase))
        {
            return (null,
                "CompletionRule 'assessment-passed' requires a quiz content (ContentType == 'quiz').", 400);
        }

        return (content, null, 0);
    }

    /// <summary>V-S13 — ConceptNodeId (optional) must resolve to a live, non-archived, same-tenant node. The node is
    /// never mutated.</summary>
    public static async Task<string?> ValidateConceptNodeAsync(
        IConceptNodeRepository nodes, Guid tenantId, Guid? conceptNodeId, CancellationToken ct)
    {
        if (conceptNodeId is not { } id || id == Guid.Empty)
        {
            return null;
        }

        var node = await nodes.GetByIdAsync(tenantId, id, ct);
        return node is null || node.IsArchived()
            ? "ConceptNodeId must reference a live, non-archived concept node in this tenant."
            : null;
    }

    /// <summary>V-S02 — a published path's step set is frozen; step add/update/archive returns 409.</summary>
    public static string? EnsureNotFrozen(KnowledgePath path)
        => path.IsStepSetFrozen()
            ? "This path version is published and its step set is frozen. Create a new version to change the steps."
            : null;

    public static List<KnowledgePathBranchCondition> MapBranchConditions(
        IReadOnlyList<KnowledgePathBranchConditionInput>? input)
        => (input ?? Array.Empty<KnowledgePathBranchConditionInput>())
            .Select(b => new KnowledgePathBranchCondition
            {
                ConditionCode = b.ConditionCode.Trim(),
                Description = KnowledgePathValidation.Trim(b.Description),
                TargetStepId = b.TargetStepId
            })
            .ToList();
}

public sealed class CreateKnowledgePathHandler : IRequestHandler<CreateKnowledgePathCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;
    private readonly ISubjectRepository _subjects;
    private readonly ITopicRepository _topics;
    private readonly IAudienceProfileRepository _profiles;

    public CreateKnowledgePathHandler(
        ITenantContext tenant, IActorContext actor, IKnowledgePathRepository paths,
        ISubjectRepository subjects, ITopicRepository topics, IAudienceProfileRepository profiles)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
        _subjects = subjects;
        _topics = topics;
        _profiles = profiles;
    }

    public async Task<Response<Guid>> Handle(CreateKnowledgePathCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var error = KnowledgePathValidation.ValidatePathCode(request.PathCode)
            ?? KnowledgePathValidation.ValidatePathName(request.PathName)
            ?? KnowledgePathValidation.ValidateObjective(request.Objective)
            ?? KnowledgePathValidation.ValidateDescription(request.Description)
            ?? KnowledgePathValidation.ValidatePathVersion(request.PathVersion)
            ?? KnowledgePathValidation.ValidatePathStatus(request.PathStatus)
            ?? KnowledgePathValidation.ValidateSource(request.Source)
            ?? KnowledgeValidation.ValidateEffectiveFrom(request.EffectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo)
            ?? KnowledgeValidation.ValidateRequiredSubject(request.SubjectId);
        if (error is not null)
        {
            return Response<Guid>.Fail(error, 400);
        }

        // A path is never born published — publish is a separate endpoint (D4).
        var status = KnowledgePathStatuses.Normalize(request.PathStatus);
        if (string.Equals(status, KnowledgePathStatuses.Published, StringComparison.Ordinal))
        {
            return Response<Guid>.Fail("A path cannot be created as published; use the publish endpoint (D4).", 400);
        }

        var referenceError = await KnowledgePathWrite.ValidateReferencesAsync(
            _subjects, _topics, _profiles, tenantId, request.SubjectId, request.TopicId,
            request.AudienceProfileId, cancellationToken);
        if (referenceError is not null)
        {
            return Response<Guid>.Fail(referenceError, 400);
        }

        var code = request.PathCode.Trim();
        var version = request.PathVersion.Trim();
        var existing = await _paths.ListByCodeAsync(tenantId, code, cancellationToken);
        if (existing.Any(p => !p.IsArchived()
                && string.Equals(p.PathVersion, version, StringComparison.OrdinalIgnoreCase)))
        {
            return Response<Guid>.Fail(
                $"A non-archived path already uses PathCode '{code}' version '{version}'.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new KnowledgePath
        {
            TenantId = tenantId,
            PathCode = code,
            PathName = request.PathName.Trim(),
            Description = KnowledgePathValidation.Trim(request.Description),
            SubjectId = request.SubjectId,
            TopicId = request.TopicId,
            AudienceProfileId = request.AudienceProfileId,
            Objective = request.Objective.Trim(),
            LanguageCode = KnowledgePathValidation.Trim(request.LanguageCode),
            PathVersion = version,
            PathStatus = status,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Source = KnowledgePathSources.Normalize(request.Source),
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _paths.InsertAsync(entity, cancellationToken);
        return Response<Guid>.Success(entity.Id, 201);
    }
}

public sealed class UpdateKnowledgePathHandler : IRequestHandler<UpdateKnowledgePathCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;
    private readonly ISubjectRepository _subjects;
    private readonly ITopicRepository _topics;
    private readonly IAudienceProfileRepository _profiles;

    public UpdateKnowledgePathHandler(
        ITenantContext tenant, IActorContext actor, IKnowledgePathRepository paths,
        ISubjectRepository subjects, ITopicRepository topics, IAudienceProfileRepository profiles)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
        _subjects = subjects;
        _topics = topics;
        _profiles = profiles;
    }

    public async Task<Response<bool>> Handle(UpdateKnowledgePathCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        // V-P16 — steps are managed only through the step sub-routes; a `steps` array on Update is rejected outright.
        if (request.StepsProvided)
        {
            return Response<bool>.Fail(
                "Steps cannot be written through the path update; use the step sub-routes (V-P16).", 400);
        }

        var path = await _paths.GetByIdAsync(tenantId, request.PathId, cancellationToken);
        if (path is null)
        {
            return Response<bool>.Fail("Knowledge path not found.", 404);
        }

        if (path.IsArchived())
        {
            return Response<bool>.Fail("An archived path cannot be updated.", 409);
        }

        if (request.ExpectedVersion is { } ev && ev != path.Version)
        {
            return Response<bool>.Fail("The path was modified by another writer; reload and retry.", 409);
        }

        var newStatus = KnowledgePathStatuses.Normalize(request.PathStatus ?? path.PathStatus);

        // V-P12 — publish is a separate endpoint; Update may not transition to published.
        if (string.Equals(newStatus, KnowledgePathStatuses.Published, StringComparison.Ordinal)
            && !path.IsPublished())
        {
            return Response<bool>.Fail("Use the publish endpoint to publish a path (V-P12 / D4).", 400);
        }

        var scalarError = KnowledgePathValidation.ValidatePathName(request.PathName)
            ?? KnowledgePathValidation.ValidateObjective(request.Objective)
            ?? KnowledgePathValidation.ValidateDescription(request.Description)
            ?? KnowledgePathValidation.ValidatePathVersion(request.PathVersion)
            ?? KnowledgePathValidation.ValidatePathStatus(request.PathStatus)
            ?? KnowledgePathValidation.ValidateSource(request.Source)
            ?? KnowledgeValidation.ValidateEffectiveFrom(request.EffectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo)
            ?? KnowledgeValidation.ValidateRequiredSubject(request.SubjectId);
        if (scalarError is not null)
        {
            return Response<bool>.Fail(scalarError, 400);
        }

        // V-P13 — a published version is frozen: only EffectiveTo and a lifecycle move (inactive/archived) may change.
        if (path.IsStepSetFrozen())
        {
            var onlyAllowedChanges =
                path.PathName == request.PathName.Trim()
                && path.SubjectId == request.SubjectId
                && path.TopicId == request.TopicId
                && path.AudienceProfileId == request.AudienceProfileId
                && path.Objective == request.Objective.Trim()
                && string.Equals(path.LanguageCode, KnowledgePathValidation.Trim(request.LanguageCode))
                && string.Equals(path.PathVersion, request.PathVersion.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(path.Description, KnowledgePathValidation.Trim(request.Description))
                && string.Equals(path.Source, KnowledgePathSources.Normalize(request.Source), StringComparison.Ordinal)
                && path.EffectiveFrom == request.EffectiveFrom;

            var statusAllowed = string.Equals(newStatus, path.PathStatus, StringComparison.Ordinal)
                || newStatus is KnowledgePathStatuses.Inactive or KnowledgePathStatuses.Archived;

            if (!onlyAllowedChanges || !statusAllowed)
            {
                return Response<bool>.Fail(
                    "A published path version is frozen; only EffectiveTo and a lifecycle change are allowed. " +
                    "Create a new version to change it (V-P13).", 409);
            }
        }
        else
        {
            var referenceError = await KnowledgePathWrite.ValidateReferencesAsync(
                _subjects, _topics, _profiles, tenantId, request.SubjectId, request.TopicId,
                request.AudienceProfileId, cancellationToken);
            if (referenceError is not null)
            {
                return Response<bool>.Fail(referenceError, 400);
            }
        }

        path.PathName = request.PathName.Trim();
        path.Description = KnowledgePathValidation.Trim(request.Description);
        path.SubjectId = request.SubjectId;
        path.TopicId = request.TopicId;
        path.AudienceProfileId = request.AudienceProfileId;
        path.Objective = request.Objective.Trim();
        path.LanguageCode = KnowledgePathValidation.Trim(request.LanguageCode);
        path.PathVersion = request.PathVersion.Trim();
        path.PathStatus = newStatus;
        path.EffectiveFrom = request.EffectiveFrom;
        path.EffectiveTo = request.EffectiveTo;
        path.Source = KnowledgePathSources.Normalize(request.Source);
        path.UpdatedAt = DateTimeOffset.UtcNow;
        path.UpdatedBy = _actor.ActorName;

        return await SaveAsync(path, cancellationToken);
    }

    private async Task<Response<bool>> SaveAsync(KnowledgePath path, CancellationToken ct)
    {
        var ok = await _paths.ReplaceAsync(path, path.Version, ct);
        return ok
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The path was modified by another writer; reload and retry.", 409);
    }
}

public sealed class PublishKnowledgePathHandler : IRequestHandler<PublishKnowledgePathCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;

    public PublishKnowledgePathHandler(ITenantContext tenant, IActorContext actor, IKnowledgePathRepository paths)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
    }

    public async Task<Response<bool>> Handle(PublishKnowledgePathCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var path = await _paths.GetByIdAsync(tenantId, request.PathId, cancellationToken);
        if (path is null)
        {
            return Response<bool>.Fail("Knowledge path not found.", 404);
        }

        if (path.IsArchived())
        {
            return Response<bool>.Fail("An archived path cannot be published.", 409);
        }

        if (request.ExpectedVersion is { } ev && ev != path.Version)
        {
            return Response<bool>.Fail("The path was modified by another writer; reload and retry.", 409);
        }

        if (path.IsPublished())
        {
            return Response<bool>.Success(true); // idempotent — already published + frozen
        }

        // V-P11 — a published path must carry at least one active, required step.
        if (!path.ActiveSteps().Any(s => s.IsRequired))
        {
            return Response<bool>.Fail(
                "A path can only be published with at least one active, required step (V-P11).", 400);
        }

        // V-P10 — no second published version of the same (PathCode, LanguageCode) may overlap in effective window.
        var siblings = await _paths.ListByCodeAsync(tenantId, path.PathCode, cancellationToken);
        var overlap = siblings.Any(other =>
            other.Id != path.Id
            && !other.IsArchived()
            && other.IsPublished()
            && string.Equals(other.LanguageCode, path.LanguageCode, StringComparison.OrdinalIgnoreCase)
            && EffectiveWindowsOverlap(other, path));
        if (overlap)
        {
            return Response<bool>.Fail(
                "Another published version of this PathCode already overlaps this effective window (V-P10).", 409);
        }

        var now = DateTimeOffset.UtcNow;
        path.PathStatus = KnowledgePathStatuses.Published;
        path.StepSetFrozenAt = now;
        path.PublishedAt = now;
        path.PublishedBy = _actor.ActorName;
        path.UpdatedAt = now;
        path.UpdatedBy = _actor.ActorName;

        var ok = await _paths.ReplaceAsync(path, path.Version, cancellationToken);
        return ok
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The path was modified by another writer; reload and retry.", 409);
    }

    private static bool EffectiveWindowsOverlap(KnowledgePath a, KnowledgePath b)
    {
        var aStart = a.EffectiveFrom;
        var aEnd = a.EffectiveTo ?? DateTimeOffset.MaxValue;
        var bStart = b.EffectiveFrom;
        var bEnd = b.EffectiveTo ?? DateTimeOffset.MaxValue;
        return aStart <= bEnd && bStart <= aEnd;
    }
}

public sealed class CreateKnowledgePathVersionHandler
    : IRequestHandler<CreateKnowledgePathVersionCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;

    public CreateKnowledgePathVersionHandler(
        ITenantContext tenant, IActorContext actor, IKnowledgePathRepository paths)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
    }

    public async Task<Response<Guid>> Handle(
        CreateKnowledgePathVersionCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var source = await _paths.GetByIdAsync(tenantId, request.PathId, cancellationToken);
        if (source is null)
        {
            return Response<Guid>.Fail("Knowledge path not found.", 404);
        }

        // V-P14 — only a published version carries a frozen step set worth cloning.
        if (!source.IsPublished())
        {
            return Response<Guid>.Fail("Only a published path version can be used to create a new version (V-P14).", 400);
        }

        var siblings = await _paths.ListByCodeAsync(tenantId, source.PathCode, cancellationToken);
        var takenVersions = siblings
            .Where(p => !p.IsArchived())
            .Select(p => p.PathVersion)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string newVersion;
        if (!string.IsNullOrWhiteSpace(request.NewPathVersion))
        {
            newVersion = request.NewPathVersion.Trim();
            if (takenVersions.Contains(newVersion))
            {
                return Response<Guid>.Fail(
                    $"A non-archived path already uses PathCode '{source.PathCode}' version '{newVersion}'.", 409);
            }
        }
        else
        {
            newVersion = NextFreeVersion(source.PathVersion, takenVersions);
        }

        // Copy steps with NEW StepIds; remap prerequisite + branch targets through the old→new id map.
        var idMap = source.Steps.ToDictionary(s => s.StepId, _ => Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;
        var copiedSteps = source.Steps.Select(s => new KnowledgePathStep
        {
            StepId = idMap[s.StepId],
            StepOrder = s.StepOrder,
            StepCode = s.StepCode,
            StepTitle = s.StepTitle,
            StepType = s.StepType,
            ContentId = s.ContentId,
            ContentCode = s.ContentCode,
            VersionPinPolicy = s.VersionPinPolicy,
            IsRequired = s.IsRequired,
            CompletionRule = s.CompletionRule,
            PrerequisiteStepId = s.PrerequisiteStepId is { } p && idMap.TryGetValue(p, out var np) ? np : null,
            ConceptNodeId = s.ConceptNodeId,
            EstimatedDurationMinutes = s.EstimatedDurationMinutes,
            Notes = s.Notes,
            BranchConditions = s.BranchConditions.Select(b => new KnowledgePathBranchCondition
            {
                ConditionCode = b.ConditionCode,
                Description = b.Description,
                TargetStepId = b.TargetStepId is { } t && idMap.TryGetValue(t, out var nt) ? nt : b.TargetStepId
            }).ToList(),
            StepStatus = s.StepStatus,
            ArchivedAt = s.ArchivedAt,
            ArchivedBy = s.ArchivedBy,
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        }).ToList();

        var clone = new KnowledgePath
        {
            TenantId = tenantId,
            PathCode = source.PathCode,
            PathName = source.PathName,
            Description = source.Description,
            SubjectId = source.SubjectId,
            TopicId = source.TopicId,
            AudienceProfileId = source.AudienceProfileId,
            Objective = source.Objective,
            LanguageCode = source.LanguageCode,
            PathVersion = newVersion,
            PathStatus = KnowledgePathStatuses.Draft,
            EffectiveFrom = source.EffectiveFrom,
            EffectiveTo = source.EffectiveTo,
            Source = source.Source,
            Steps = copiedSteps,
            SupersedesPathId = source.Id,
            StepSetFrozenAt = null,
            PublishedAt = null,
            PublishedBy = null,
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _paths.InsertAsync(clone, cancellationToken);
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

public sealed class ArchiveKnowledgePathHandler : IRequestHandler<ArchiveKnowledgePathCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;

    public ArchiveKnowledgePathHandler(ITenantContext tenant, IActorContext actor, IKnowledgePathRepository paths)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
    }

    public async Task<Response<bool>> Handle(ArchiveKnowledgePathCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var path = await _paths.GetByIdAsync(tenantId, request.PathId, cancellationToken);
        if (path is null)
        {
            return Response<bool>.Fail("Knowledge path not found.", 404);
        }

        if (path.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        if (request.ExpectedVersion is { } ev && ev != path.Version)
        {
            return Response<bool>.Fail("The path was modified by another writer; reload and retry.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        path.PathStatus = KnowledgePathStatuses.Archived;
        path.ArchivedAt = now;
        path.ArchivedBy = _actor.ActorName;
        path.UpdatedAt = now;
        path.UpdatedBy = _actor.ActorName;

        var ok = await _paths.ReplaceAsync(path, path.Version, cancellationToken);
        return ok
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The path was modified by another writer; reload and retry.", 409);
    }
}
