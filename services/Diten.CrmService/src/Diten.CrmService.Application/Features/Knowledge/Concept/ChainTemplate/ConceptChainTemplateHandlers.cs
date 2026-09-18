using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using ChainTemplateEntity = Diten.CrmService.Domain.Entities.ConceptChainTemplate;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.ChainTemplate;

/// <summary>Shared chain-template logic: same-subject membership check for the ordered types, and the published
/// effective-window overlap guard (two published versions of one ChainCode may not overlap).</summary>
internal static class ConceptChainTemplateRules
{
    public static async Task<string?> ValidateOrderedTypesBelongToSubjectAsync(
        IConceptTypeRepository types,
        Guid tenantId,
        Guid subjectId,
        IReadOnlyList<Guid> orderedConceptTypes,
        CancellationToken cancellationToken)
    {
        var subjectTypeIds = (await types.ListBySubjectAsync(tenantId, subjectId, cancellationToken))
            .Select(t => t.Id)
            .ToHashSet();

        return orderedConceptTypes.All(subjectTypeIds.Contains)
            ? null
            : "OrderedConceptTypes must all be concept types of the same subject.";
    }

    /// <summary>Two effective windows overlap when each starts on or before the other ends (open-ended = MaxValue).</summary>
    public static bool WindowsOverlap(
        DateTimeOffset aFrom, DateTimeOffset? aTo, DateTimeOffset bFrom, DateTimeOffset? bTo)
        => aFrom <= (bTo ?? DateTimeOffset.MaxValue) && bFrom <= (aTo ?? DateTimeOffset.MaxValue);

    public static ChainTemplateEntity? FindPublishedOverlap(
        IReadOnlyList<ChainTemplateEntity> sameCode,
        Guid? selfId,
        DateTimeOffset from,
        DateTimeOffset? to)
        => sameCode.FirstOrDefault(t =>
            (selfId is null || t.Id != selfId)
            && !t.IsArchived()
            && t.IsPublished()
            && WindowsOverlap(from, to, t.EffectiveFrom, t.EffectiveTo));

    // ─── SCMM-10 (③, RM2) branched structure ─────────────────────────────────

    /// <summary>Structural validation of the optional branch list (400 message or null). Legacy templates send no
    /// branches and skip this. No engine is opened — cardinality/refs are stored, never evaluated (D8).</summary>
    public static string? ValidateBranchesShape(IReadOnlyList<ConceptChainBranchInput>? branches)
    {
        if (branches is null || branches.Count == 0)
        {
            return null;
        }

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var branch in branches)
        {
            var code = branch.BranchCode?.Trim() ?? string.Empty;
            if (code.Length == 0)
            {
                return "Each branch requires a non-empty BranchCode.";
            }

            if (!codes.Add(code))
            {
                return $"Branch code '{code}' is used more than once in the template.";
            }

            if (branch.Steps is null || branch.Steps.Count == 0)
            {
                return $"Branch '{code}' must contain at least one step.";
            }

            var typesInBranch = new HashSet<Guid>();
            foreach (var step in branch.Steps)
            {
                if (step.ConceptTypeId == Guid.Empty)
                {
                    return $"Branch '{code}' has a step with an empty ConceptTypeId.";
                }

                if (!typesInBranch.Add(step.ConceptTypeId))
                {
                    return $"Branch '{code}' uses the same concept type twice in one branch.";
                }

                if (step.MinSelection < 0)
                {
                    return $"Branch '{code}': MinSelection cannot be negative.";
                }

                if (step.MaxSelection is { } max && (max < 1 || max < step.MinSelection))
                {
                    return $"Branch '{code}': MaxSelection must be null, or at least 1 and not less than MinSelection.";
                }
            }
        }

        return null;
    }

    /// <summary>Distinct concept-type ids across all branch steps — used for the same-subject membership check.</summary>
    public static IReadOnlyList<Guid> BranchTypeIds(IReadOnlyList<ConceptChainBranchInput> branches)
        => branches.SelectMany(b => b.Steps).Select(s => s.ConceptTypeId).Distinct().ToList();

    /// <summary>Maps the branch input to the embedded domain value objects (structure only — no step-level refs).</summary>
    public static List<ConceptChainBranch> ToDomain(IReadOnlyList<ConceptChainBranchInput>? branches)
        => (branches ?? Array.Empty<ConceptChainBranchInput>()).Select(b => new ConceptChainBranch
        {
            BranchCode = b.BranchCode.Trim(),
            BranchName = string.IsNullOrWhiteSpace(b.BranchName) ? null : b.BranchName.Trim(),
            SortOrder = b.SortOrder,
            Steps = (b.Steps ?? Array.Empty<ConceptChainStepInput>()).Select(s => new ConceptChainStep
            {
                ConceptTypeId = s.ConceptTypeId,
                MinSelection = s.MinSelection,
                MaxSelection = s.MaxSelection
            }).ToList()
        }).ToList();

    // ─── SCMM-10 (WP-A) template-level Moderator / ForWhom ───────────────────

    /// <summary>Trims the moderator role type; blank → null (vocabulary is validated by the reference set, WP-B — the
    /// backend only stores a trimmed, non-empty value).</summary>
    public static string? NormalizeModerator(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Distinct, non-empty for-whom ids (order-insensitive config list).</summary>
    public static List<Guid> NormalizeForWhom(IReadOnlyList<Guid>? ids)
        => (ids ?? Array.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList();

    /// <summary>ForWhom guard (400 message or null): every supplied AudienceProfile id must exist and be non-archived.
    /// Empty list is valid. Fail-closed and run BEFORE persist (the <c>ValidateSubjectAsync</c> pattern).</summary>
    public static async Task<string?> ValidateForWhomAsync(
        IAudienceProfileRepository profiles,
        Guid tenantId,
        IReadOnlyList<Guid> forWhomAudienceProfileIds,
        CancellationToken cancellationToken)
    {
        foreach (var id in forWhomAudienceProfileIds)
        {
            var profile = await profiles.GetByIdAsync(tenantId, id, cancellationToken);
            if (profile is null)
            {
                return $"ForWhomAudienceProfileIds references an audience profile that does not exist ({id}).";
            }

            if (profile.IsArchived())
            {
                return $"ForWhomAudienceProfileIds cannot reference an archived audience profile ({id}).";
            }
        }

        return null;
    }

    /// <summary>Order-insensitive set equality of two for-whom id lists — the template-level freeze guard.</summary>
    public static bool ForWhomEqual(IReadOnlyCollection<Guid> a, IReadOnlyCollection<Guid> b)
        => a.Count == b.Count && new HashSet<Guid>(a).SetEquals(b);

    /// <summary>Structural equality of two branch lists — the freeze guard: a published template's branch structure is
    /// frozen exactly like <c>OrderedConceptTypes</c>.</summary>
    public static bool BranchesEqual(List<ConceptChainBranch> a, List<ConceptChainBranch> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            var x = a[i];
            var y = b[i];
            if (!string.Equals(x.BranchCode, y.BranchCode, StringComparison.Ordinal)
                || (x.BranchName ?? string.Empty) != (y.BranchName ?? string.Empty)
                || x.SortOrder != y.SortOrder
                || x.Steps.Count != y.Steps.Count)
            {
                return false;
            }

            for (var j = 0; j < x.Steps.Count; j++)
            {
                var sx = x.Steps[j];
                var sy = y.Steps[j];
                if (sx.ConceptTypeId != sy.ConceptTypeId
                    || sx.MinSelection != sy.MinSelection
                    || sx.MaxSelection != sy.MaxSelection)
                {
                    return false;
                }
            }
        }

        return true;
    }
}

public sealed class CreateConceptChainTemplateHandler
    : IRequestHandler<CreateConceptChainTemplateCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IConceptChainTemplateRepository _templates;
    private readonly IConceptTypeRepository _types;
    private readonly IAudienceProfileRepository _audienceProfiles;
    private readonly IKnowledgeConceptAuditPublisher? _audit;

    public CreateConceptChainTemplateHandler(
        ITenantContext tenant,
        IActorContext actor,
        IConceptChainTemplateRepository templates,
        IConceptTypeRepository types,
        IAudienceProfileRepository audienceProfiles,
        IKnowledgeConceptAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _templates = templates;
        _types = types;
        _audienceProfiles = audienceProfiles;
        _audit = audit;
    }

    public async Task<Response<Guid>> Handle(
        CreateConceptChainTemplateCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var error = KnowledgeValidation.ValidateCode(request.ChainCode, "ChainCode")
            ?? KnowledgeValidation.ValidateName(request.ChainName, "ChainName")
            ?? ConceptGraphValidation.ValidateChainStatus(request.Status)
            ?? KnowledgeValidation.ValidateRequiredSubject(request.SubjectId)
            ?? KnowledgeValidation.ValidateEffectiveFrom(request.EffectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo)
            ?? ConceptGraphValidation.ValidateOrderedTypesShape(request.OrderedConceptTypes);
        if (error is not null)
        {
            return Response<Guid>.Fail(error, 400);
        }

        // V12 — every ordered type must belong to the subject.
        var membershipError = await ConceptChainTemplateRules.ValidateOrderedTypesBelongToSubjectAsync(
            _types, tenantId, request.SubjectId, request.OrderedConceptTypes, cancellationToken);
        if (membershipError is not null)
        {
            return Response<Guid>.Fail(membershipError, 400);
        }

        // SCMM-10 (③, RM2) — optional branch structure shape + same-subject membership.
        var branchShapeError = ConceptChainTemplateRules.ValidateBranchesShape(request.Branches);
        if (branchShapeError is not null)
        {
            return Response<Guid>.Fail(branchShapeError, 400);
        }

        if (request.Branches is { Count: > 0 })
        {
            var branchMembershipError = await ConceptChainTemplateRules.ValidateOrderedTypesBelongToSubjectAsync(
                _types, tenantId, request.SubjectId,
                ConceptChainTemplateRules.BranchTypeIds(request.Branches), cancellationToken);
            if (branchMembershipError is not null)
            {
                return Response<Guid>.Fail(branchMembershipError, 400);
            }
        }

        // SCMM-10 (WP-A, D-d) — every for-whom AudienceProfile ref must exist and be non-archived (fail-closed, pre-persist).
        var moderatorRoleType = ConceptChainTemplateRules.NormalizeModerator(request.ModeratorRoleType);
        var forWhom = ConceptChainTemplateRules.NormalizeForWhom(request.ForWhomAudienceProfileIds);
        var forWhomError = await ConceptChainTemplateRules.ValidateForWhomAsync(
            _audienceProfiles, tenantId, forWhom, cancellationToken);
        if (forWhomError is not null)
        {
            return Response<Guid>.Fail(forWhomError, 400);
        }

        // V13 — a published version must not overlap another published version of the same code.
        var status = ConceptChainStatuses.Normalize(request.Status);
        if (string.Equals(status, ConceptChainStatuses.Published, StringComparison.OrdinalIgnoreCase))
        {
            var sameCode = await _templates.ListByCodeAsync(
                tenantId, request.SubjectId, request.ChainCode.Trim(), cancellationToken);
            if (ConceptChainTemplateRules.FindPublishedOverlap(
                    sameCode, null, request.EffectiveFrom, request.EffectiveTo) is { } clash)
            {
                return Response<Guid>.Fail(
                    $"Another published version of ChainCode '{request.ChainCode.Trim()}' overlaps this effective " +
                    $"window (templateId={clash.Id}).", 409);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new ChainTemplateEntity
        {
            TenantId = tenantId,
            SubjectId = request.SubjectId,
            ChainCode = request.ChainCode.Trim(),
            ChainName = request.ChainName.Trim(),
            Description = KnowledgeValidation.Trim(request.Description),
            OrderedConceptTypes = request.OrderedConceptTypes.ToList(),
            Branches = ConceptChainTemplateRules.ToDomain(request.Branches),
            ModeratorRoleType = moderatorRoleType,
            ForWhomAudienceProfileIds = forWhom,
            Status = status,
            ChainVersion = string.IsNullOrWhiteSpace(request.ChainVersion) ? "1.0" : request.ChainVersion.Trim(),
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _templates.InsertAsync(entity, cancellationToken);
        if (_audit is not null)
        {
            var evt = entity.IsPublished()
                ? ConceptGraphReasonCodes.ChainTemplatePublished
                : ConceptGraphReasonCodes.ChainTemplateCreated;
            await _audit.PublishAsync(evt, tenantId, KnowledgeConceptAuditEntities.ConceptChainTemplate,
                entity.Id, entity.Version, entity.ChainCode, cancellationToken);
        }

        return Response<Guid>.Success(entity.Id, 201);
    }
}

public sealed class UpdateConceptChainTemplateHandler
    : IRequestHandler<UpdateConceptChainTemplateCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IConceptChainTemplateRepository _templates;
    private readonly IConceptTypeRepository _types;
    private readonly IAudienceProfileRepository _audienceProfiles;

    private readonly IKnowledgeConceptAuditPublisher? _audit;

    public UpdateConceptChainTemplateHandler(
        ITenantContext tenant,
        IActorContext actor,
        IConceptChainTemplateRepository templates,
        IConceptTypeRepository types,
        IAudienceProfileRepository audienceProfiles,
        IKnowledgeConceptAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _templates = templates;
        _types = types;
        _audienceProfiles = audienceProfiles;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(
        UpdateConceptChainTemplateCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _templates.GetByIdAsync(tenantId, request.ConceptChainTemplateId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("Concept chain template not found.", 404);
        }

        if (entity.IsArchived())
        {
            return Response<bool>.Fail("An archived concept chain template cannot be updated.", 409);
        }

        if (string.Equals(request.Status?.Trim(), ConceptChainStatuses.Archived, StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Fail("Use the archive endpoint to archive a concept chain template.", 400);
        }

        var error = KnowledgeValidation.ValidateName(request.ChainName, "ChainName")
            ?? ConceptGraphValidation.ValidateChainStatus(request.Status)
            ?? KnowledgeValidation.ValidateEffectiveFrom(request.EffectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo)
            ?? ConceptGraphValidation.ValidateOrderedTypesShape(request.OrderedConceptTypes);
        if (error is not null)
        {
            return Response<bool>.Fail(error, 400);
        }

        // SCMM-10 (③, RM2) — validate the optional branch structure before the freeze/membership checks.
        var branchShapeError = ConceptChainTemplateRules.ValidateBranchesShape(request.Branches);
        if (branchShapeError is not null)
        {
            return Response<bool>.Fail(branchShapeError, 400);
        }

        var newBranches = ConceptChainTemplateRules.ToDomain(request.Branches);
        var branchesChanged = !ConceptChainTemplateRules.BranchesEqual(entity.Branches, newBranches);

        // SCMM-10 (WP-A, D-f) — the template-level Moderator / ForWhom freeze alongside the spine + branches.
        var newModerator = ConceptChainTemplateRules.NormalizeModerator(request.ModeratorRoleType);
        var newForWhom = ConceptChainTemplateRules.NormalizeForWhom(request.ForWhomAudienceProfileIds);
        var moderatorChanged = !string.Equals(
            entity.ModeratorRoleType ?? string.Empty, newModerator ?? string.Empty, StringComparison.Ordinal);
        var forWhomChanged = !ConceptChainTemplateRules.ForWhomEqual(entity.ForWhomAudienceProfileIds, newForWhom);

        // A published version freezes its sequence, its branch structure AND its template-level Moderator / ForWhom —
        // changing any of them needs a new version.
        var sequenceChanged = !entity.OrderedConceptTypes.SequenceEqual(request.OrderedConceptTypes);
        if (entity.IsPublished() && (sequenceChanged || branchesChanged || moderatorChanged || forWhomChanged))
        {
            return Response<bool>.Fail(
                "OrderedConceptTypes, Branches, ModeratorRoleType and ForWhomAudienceProfileIds are frozen on a "
                + "published template; create a new version to change them.",
                409);
        }

        if (sequenceChanged)
        {
            var membershipError = await ConceptChainTemplateRules.ValidateOrderedTypesBelongToSubjectAsync(
                _types, tenantId, entity.SubjectId, request.OrderedConceptTypes, cancellationToken);
            if (membershipError is not null)
            {
                return Response<bool>.Fail(membershipError, 400);
            }
        }

        if (branchesChanged && request.Branches is { Count: > 0 })
        {
            var branchMembershipError = await ConceptChainTemplateRules.ValidateOrderedTypesBelongToSubjectAsync(
                _types, tenantId, entity.SubjectId,
                ConceptChainTemplateRules.BranchTypeIds(request.Branches), cancellationToken);
            if (branchMembershipError is not null)
            {
                return Response<bool>.Fail(branchMembershipError, 400);
            }
        }

        // SCMM-10 (WP-A, D-d) — when the for-whom set changes, every new ref must exist and be non-archived
        // (fail-closed, pre-persist). Unchanged sets are not re-validated, so a no-op update never fails on a ref that
        // was archived after this template first referenced it.
        if (forWhomChanged)
        {
            var forWhomError = await ConceptChainTemplateRules.ValidateForWhomAsync(
                _audienceProfiles, tenantId, newForWhom, cancellationToken);
            if (forWhomError is not null)
            {
                return Response<bool>.Fail(forWhomError, 400);
            }
        }

        // V13 — publishing must not overlap another published version of the same code.
        var status = ConceptChainStatuses.Normalize(request.Status ?? entity.Status);
        if (string.Equals(status, ConceptChainStatuses.Published, StringComparison.OrdinalIgnoreCase))
        {
            var sameCode = await _templates.ListByCodeAsync(
                tenantId, entity.SubjectId, entity.ChainCode, cancellationToken);
            if (ConceptChainTemplateRules.FindPublishedOverlap(
                    sameCode, entity.Id, request.EffectiveFrom, request.EffectiveTo) is { } clash)
            {
                return Response<bool>.Fail(
                    $"Another published version of ChainCode '{entity.ChainCode}' overlaps this effective window " +
                    $"(templateId={clash.Id}).", 409);
            }
        }

        var wasPublished = entity.IsPublished();
        var now = DateTimeOffset.UtcNow;
        entity.ChainName = request.ChainName.Trim();
        entity.Description = KnowledgeValidation.Trim(request.Description);
        entity.OrderedConceptTypes = request.OrderedConceptTypes.ToList();
        entity.Branches = newBranches;
        entity.ModeratorRoleType = newModerator;
        entity.ForWhomAudienceProfileIds = newForWhom;
        entity.Status = status;
        if (!string.IsNullOrWhiteSpace(request.ChainVersion))
        {
            entity.ChainVersion = request.ChainVersion.Trim();
        }

        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        entity.UpdatedAt = now;
        entity.UpdatedBy = _actor.ActorName;

        await _templates.UpdateAsync(entity, cancellationToken);
        if (_audit is not null)
        {
            // A draft→published transition is its own audit event; anything else is a plain update.
            var evt = entity.IsPublished() && !wasPublished
                ? ConceptGraphReasonCodes.ChainTemplatePublished
                : ConceptGraphReasonCodes.ChainTemplateUpdated;
            await _audit.PublishAsync(evt, tenantId, KnowledgeConceptAuditEntities.ConceptChainTemplate,
                entity.Id, entity.Version, entity.ChainCode, cancellationToken);
        }

        return Response<bool>.Success(true);
    }
}

public sealed class ArchiveConceptChainTemplateHandler
    : IRequestHandler<ArchiveConceptChainTemplateCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IConceptChainTemplateRepository _templates;
    private readonly IKnowledgeConceptAuditPublisher? _audit;

    public ArchiveConceptChainTemplateHandler(
        ITenantContext tenant, IActorContext actor, IConceptChainTemplateRepository templates,
        IKnowledgeConceptAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _templates = templates;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(
        ArchiveConceptChainTemplateCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _templates.GetByIdAsync(tenantId, request.ConceptChainTemplateId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("Concept chain template not found.", 404);
        }

        if (entity.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        var now = DateTimeOffset.UtcNow;
        entity.Status = ConceptChainStatuses.Archived;
        entity.ArchivedAt = now;
        entity.ArchivedBy = _actor.ActorName;
        entity.UpdatedAt = now;
        entity.UpdatedBy = _actor.ActorName;

        await _templates.UpdateAsync(entity, cancellationToken);
        if (_audit is not null)
        {
            await _audit.PublishAsync(ConceptGraphReasonCodes.ChainTemplateArchived, tenantId,
                KnowledgeConceptAuditEntities.ConceptChainTemplate, entity.Id, entity.Version, entity.ChainCode, cancellationToken);
        }

        return Response<bool>.Success(true);
    }
}

public sealed class ListConceptChainTemplatesHandler
    : IRequestHandler<ListConceptChainTemplatesQuery, Response<ConceptChainTemplateListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IConceptChainTemplateRepository _templates;

    public ListConceptChainTemplatesHandler(ITenantContext tenant, IConceptChainTemplateRepository templates)
    {
        _tenant = tenant;
        _templates = templates;
    }

    public async Task<Response<ConceptChainTemplateListDto>> Handle(
        ListConceptChainTemplatesQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ConceptChainTemplateListDto>.Fail("Tenant context is required.", 400);
        }

        IEnumerable<ChainTemplateEntity> rows = request.SubjectId is { } subjectId && subjectId != Guid.Empty
            ? await _templates.ListBySubjectAsync(tenantId, subjectId, cancellationToken)
            : await _templates.ListAsync(tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = ConceptChainStatuses.Normalize(request.Status);
            rows = rows.Where(x => x.Status == status);
        }

        if (request.EffectiveAt is { } at)
        {
            rows = rows.Where(x => x.EffectiveFrom <= at && (x.EffectiveTo is null || at <= x.EffectiveTo));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            rows = rows.Where(x =>
                x.ChainName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.ChainCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(x => !x.IsArchived());
        }

        var items = rows.Select(ConceptGraphMapper.ToDto).ToList();
        return Response<ConceptChainTemplateListDto>.Success(new ConceptChainTemplateListDto(items, items.Count));
    }
}

public sealed class GetConceptChainTemplateHandler
    : IRequestHandler<GetConceptChainTemplateQuery, Response<ConceptChainTemplateDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IConceptChainTemplateRepository _templates;

    public GetConceptChainTemplateHandler(ITenantContext tenant, IConceptChainTemplateRepository templates)
    {
        _tenant = tenant;
        _templates = templates;
    }

    public async Task<Response<ConceptChainTemplateDto>> Handle(
        GetConceptChainTemplateQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ConceptChainTemplateDto>.Fail("Tenant context is required.", 400);
        }

        var entity = await _templates.GetByIdAsync(tenantId, request.ConceptChainTemplateId, cancellationToken);
        return entity is null
            ? Response<ConceptChainTemplateDto>.Fail("Concept chain template not found.", 404)
            : Response<ConceptChainTemplateDto>.Success(ConceptGraphMapper.ToDto(entity));
    }
}
