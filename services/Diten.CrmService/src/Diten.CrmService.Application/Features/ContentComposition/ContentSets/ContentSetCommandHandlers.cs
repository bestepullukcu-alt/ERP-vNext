using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentSets;

/// <summary>
/// SCMM-14 arrangement rules. A <see cref="ConceptChainStep"/> carries no synthetic id, so an arrangement addresses a
/// step by its <c>ConceptTypeId</c> within a branch: <c>TemplateStepId</c> = the target step's concept-type id,
/// <c>BranchId</c> = the branch's <see cref="ConceptChainBranch.BranchCode"/> (null on a legacy single-line template that
/// uses <see cref="ConceptChainTemplate.OrderedConceptTypes"/>). Per-position <c>MaxSelection</c> caps how many
/// COMPONENTS may sit in one branch slot; the legacy spine has no per-position cardinality. Claims are placed
/// positionally but are not the chain's typed nodes, so no cardinality applies to them.
/// </summary>
internal static class ContentSetArrangement
{
    public static (string? Error, int Status) ValidateSlot(
        ConceptChainTemplate template, Guid templateStepId, string? branchId, int position)
    {
        if (position < 0)
        {
            return ("Position must be zero or greater.", 400);
        }

        if (templateStepId == Guid.Empty)
        {
            return ("TemplateStepId is required.", 400);
        }

        if (template.Branches is { Count: > 0 })
        {
            if (string.IsNullOrWhiteSpace(branchId))
            {
                return ("This template declares branches; branchId is required.", 400);
            }

            var branch = template.Branches.FirstOrDefault(b =>
                string.Equals(b.BranchCode, branchId, StringComparison.OrdinalIgnoreCase));
            if (branch is null)
            {
                return ($"Unknown branchId '{branchId}' for this template.", 400);
            }

            if (!branch.Steps.Any(s => s.ConceptTypeId == templateStepId))
            {
                return ("TemplateStepId does not match any step (concept type) in the branch.", 400);
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(branchId))
            {
                return ("This template has no branches; branchId must be omitted.", 400);
            }

            if (!template.OrderedConceptTypes.Contains(templateStepId))
            {
                return ("TemplateStepId does not match any step in the template.", 400);
            }
        }

        return (null, 0);
    }

    /// <summary>The per-position component cap for the target slot, or null (spine mode / unbounded).</summary>
    public static int? MaxSelectionFor(ConceptChainTemplate template, Guid templateStepId, string? branchId)
    {
        if (template.Branches is not { Count: > 0 } || string.IsNullOrWhiteSpace(branchId))
        {
            return null;
        }

        var branch = template.Branches.FirstOrDefault(b =>
            string.Equals(b.BranchCode, branchId, StringComparison.OrdinalIgnoreCase));
        return branch?.Steps.FirstOrDefault(s => s.ConceptTypeId == templateStepId)?.MaxSelection;
    }

    public static bool SameSlot(ContentArrangement a, Guid templateStepId, string? branchId)
        => a.TemplateStepId == templateStepId
           && string.Equals(a.BranchId ?? string.Empty, branchId ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Base for the ContentSet write handlers: shared tenant/set loading + audit emission.</summary>
public abstract class ContentSetWriteHandlerBase
{
    protected readonly ITenantContext Tenant;
    protected readonly IActorContext Actor;
    protected readonly IContentSetRepository Sets;
    protected readonly IContentCompositionAuditPublisher? Audit;

    protected ContentSetWriteHandlerBase(
        ITenantContext tenant, IActorContext actor, IContentSetRepository sets, IContentCompositionAuditPublisher? audit)
    {
        Tenant = tenant;
        Actor = actor;
        Sets = sets;
        Audit = audit;
    }

    protected async Task PublishAsync(string reason, Guid tenantId, ContentSet set, CancellationToken ct)
    {
        if (Audit is not null)
        {
            await Audit.PublishAsync(reason, tenantId, ContentCompositionAuditEntities.ContentSet,
                set.Id, set.Version, set.SetCode, ct);
        }
    }

    /// <summary>Loads a non-archived set for mutation. Returns (null, error-response) on 400/404/409.</summary>
    protected async Task<(ContentSet? Set, Guid TenantId, string? Error, int Status)> LoadEditableAsync(
        Guid contentSetId, CancellationToken ct)
    {
        if (Tenant.TenantId is not { } tenantId)
        {
            return (null, Guid.Empty, "Tenant context is required.", 400);
        }

        var set = await Sets.GetByIdAsync(tenantId, contentSetId, ct);
        if (set is null)
        {
            return (null, tenantId, "Content set not found.", 404);
        }

        if (set.IsArchived())
        {
            return (null, tenantId, "An archived content set cannot be modified.", 409);
        }

        return (set, tenantId, null, 0);
    }

    protected void Stamp(ContentSet set)
    {
        set.UpdatedAt = DateTimeOffset.UtcNow;
        set.UpdatedBy = Actor.ActorName;
    }
}

public sealed class CreateContentSetDraftHandler : IRequestHandler<CreateContentSetDraftCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IContentSetRepository _sets;
    private readonly IConceptChainTemplateRepository _templates;
    private readonly IContentScopeRepository _scopes;
    private readonly IContentCompositionAuditPublisher? _audit;

    public CreateContentSetDraftHandler(
        ITenantContext tenant, IActorContext actor, IContentSetRepository sets,
        IConceptChainTemplateRepository templates, IContentScopeRepository scopes,
        IContentCompositionAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _sets = sets;
        _templates = templates;
        _scopes = scopes;
        _audit = audit;
    }

    public async Task<Response<Guid>> Handle(CreateContentSetDraftCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.SetCode))
        {
            return Response<Guid>.Fail("SetCode is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.SetName))
        {
            return Response<Guid>.Fail("SetName is required.", 400);
        }

        var code = request.SetCode.Trim();
        if (await _sets.GetActiveByCodeAsync(tenantId, code, cancellationToken) is { } duplicate)
        {
            return Response<Guid>.Fail(
                $"A non-archived content set already uses SetCode '{code}' (contentSetId={duplicate.Id}).", 409);
        }

        var template = await _templates.GetByIdAsync(tenantId, request.ConceptChainTemplateId, cancellationToken);
        if (template is null)
        {
            return Response<Guid>.Fail("ConceptChainTemplateId does not reference a template in this tenant.", 400);
        }

        if (template.IsArchived())
        {
            return Response<Guid>.Fail("Cannot build a set on an archived composition template.", 409);
        }

        ContentSetScopeRef? scopeRef = null;
        if (request.ContentScopeId is { } scopeId && scopeId != Guid.Empty)
        {
            var scope = await _scopes.GetByIdAsync(tenantId, scopeId, cancellationToken);
            if (scope is null)
            {
                return Response<Guid>.Fail("ContentScopeId does not reference a scope in this tenant.", 400);
            }

            if (scope.IsArchived())
            {
                return Response<Guid>.Fail("Cannot bind an archived content scope.", 409);
            }

            scopeRef = new ContentSetScopeRef { ContentScopeId = scope.Id, ScopeVersion = scope.ScopeVersion };
        }

        var now = DateTimeOffset.UtcNow;
        var set = new ContentSet
        {
            TenantId = tenantId,
            SetCode = code,
            SetName = request.SetName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            // Ref PIN (D14-d): the template's business version is snapshotted at selection time.
            Template = new ContentSetTemplateRef
            {
                ConceptChainTemplateId = template.Id,
                ChainVersion = template.ChainVersion
            },
            Scope = scopeRef,
            DraftSchemaVersion = 1,
            Status = ContentSetStatuses.Draft,
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _sets.InsertAsync(set, cancellationToken);
        if (_audit is not null)
        {
            await _audit.PublishAsync(ContentSetReasonCodes.Created, tenantId,
                ContentCompositionAuditEntities.ContentSet, set.Id, set.Version, set.SetCode, cancellationToken);
        }

        return Response<Guid>.Success(set.Id, 201);
    }
}

public sealed class CloneContentSetToDraftHandler : ContentSetWriteHandlerBase,
    IRequestHandler<CloneContentSetToDraftCommand, Response<Guid>>
{
    public CloneContentSetToDraftHandler(
        ITenantContext tenant, IActorContext actor, IContentSetRepository sets,
        IContentCompositionAuditPublisher? audit = null)
        : base(tenant, actor, sets, audit) { }

    public async Task<Response<Guid>> Handle(CloneContentSetToDraftCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.NewSetCode))
        {
            return Response<Guid>.Fail("NewSetCode is required.", 400);
        }

        var source = await Sets.GetByIdAsync(tenantId, request.SourceContentSetId, cancellationToken);
        if (source is null)
        {
            return Response<Guid>.Fail("Source content set not found.", 404);
        }

        var code = request.NewSetCode.Trim();
        if (await Sets.GetActiveByCodeAsync(tenantId, code, cancellationToken) is { } duplicate)
        {
            return Response<Guid>.Fail(
                $"A non-archived content set already uses SetCode '{code}' (contentSetId={duplicate.Id}).", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var clone = new ContentSet
        {
            TenantId = tenantId,
            SetCode = code,
            SetName = string.IsNullOrWhiteSpace(request.NewSetName) ? $"{source.SetName} (copy)" : request.NewSetName.Trim(),
            Description = source.Description,
            // Refs remap onto a new record; the pinned versions are preserved (provenance travels with the clone).
            Template = new ContentSetTemplateRef
            {
                ConceptChainTemplateId = source.Template.ConceptChainTemplateId,
                ChainVersion = source.Template.ChainVersion
            },
            Scope = source.Scope is null ? null : new ContentSetScopeRef
            {
                ContentScopeId = source.Scope.ContentScopeId,
                ScopeVersion = source.Scope.ScopeVersion
            },
            SelectedComponents = source.SelectedComponents.Select(c => new ContentSetComponent
            {
                SelectionId = Guid.NewGuid(),   // fresh selection identity on the clone
                KnowledgeContentId = c.KnowledgeContentId,
                ContentVersion = c.ContentVersion,
                LanguageCode = c.LanguageCode,
                Role = c.Role,
                Arrangement = new ContentArrangement
                {
                    TemplateStepId = c.Arrangement.TemplateStepId,
                    BranchId = c.Arrangement.BranchId,
                    Position = c.Arrangement.Position
                }
            }).ToList(),
            SelectedClaims = source.SelectedClaims.Select(c => new ContentSetClaim
            {
                SelectionId = Guid.NewGuid(),
                ClaimId = c.ClaimId,
                ClaimVersion = c.ClaimVersion,
                Arrangement = new ContentArrangement
                {
                    TemplateStepId = c.Arrangement.TemplateStepId,
                    BranchId = c.Arrangement.BranchId,
                    Position = c.Arrangement.Position
                }
            }).ToList(),
            DraftSchemaVersion = source.DraftSchemaVersion,
            // NO-INHERITED-APPROVAL (docx): the clone is a fresh draft and carries no validation snapshot.
            Status = ContentSetStatuses.Draft,
            EligibilitySnapshot = null,
            CreatedAt = now,
            CreatedBy = Actor.ActorName
        };

        await Sets.InsertAsync(clone, cancellationToken);
        await PublishAsync(ContentSetReasonCodes.Cloned, tenantId, clone, cancellationToken);
        return Response<Guid>.Success(clone.Id, 201);
    }
}

public sealed class UpdateContentSetHandler : ContentSetWriteHandlerBase,
    IRequestHandler<UpdateContentSetCommand, Response<bool>>
{
    public UpdateContentSetHandler(
        ITenantContext tenant, IActorContext actor, IContentSetRepository sets,
        IContentCompositionAuditPublisher? audit = null)
        : base(tenant, actor, sets, audit) { }

    public async Task<Response<bool>> Handle(UpdateContentSetCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (set, tenantId, error, status) = await LoadEditableAsync(request.ContentSetId, cancellationToken);
        if (set is null)
        {
            return Response<bool>.Fail(error!, status);
        }

        if (string.IsNullOrWhiteSpace(request.SetName))
        {
            return Response<bool>.Fail("SetName is required.", 400);
        }

        if (!string.IsNullOrWhiteSpace(request.Status)
            && (!ContentSetStatuses.IsValid(request.Status)
                || string.Equals(request.Status.Trim(), ContentSetStatuses.Archived, StringComparison.OrdinalIgnoreCase)))
        {
            return Response<bool>.Fail(
                $"Status must be one of: {ContentSetStatuses.Draft}, {ContentSetStatuses.Inactive} "
                + "(use the archive endpoint to archive).", 400);
        }

        set.SetName = request.SetName.Trim();
        set.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        set.Status = ContentSetStatuses.Normalize(request.Status ?? set.Status);
        Stamp(set);

        await Sets.UpdateAsync(set, cancellationToken);
        await PublishAsync(ContentSetReasonCodes.Updated, tenantId, set, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ArchiveContentSetHandler : ContentSetWriteHandlerBase,
    IRequestHandler<ArchiveContentSetCommand, Response<bool>>
{
    public ArchiveContentSetHandler(
        ITenantContext tenant, IActorContext actor, IContentSetRepository sets,
        IContentCompositionAuditPublisher? audit = null)
        : base(tenant, actor, sets, audit) { }

    public async Task<Response<bool>> Handle(ArchiveContentSetCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var set = await Sets.GetByIdAsync(tenantId, request.ContentSetId, cancellationToken);
        if (set is null)
        {
            return Response<bool>.Fail("Content set not found.", 404);
        }

        if (set.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        var now = DateTimeOffset.UtcNow;
        set.Status = ContentSetStatuses.Archived;
        set.ArchivedAt = now;
        set.ArchivedBy = Actor.ActorName;
        Stamp(set);

        await Sets.UpdateAsync(set, cancellationToken);
        await PublishAsync(ContentSetReasonCodes.Archived, tenantId, set, cancellationToken);
        return Response<bool>.Success(true);
    }
}
