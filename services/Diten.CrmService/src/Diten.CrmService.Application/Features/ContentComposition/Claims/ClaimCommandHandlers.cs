using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

/// <summary>SCMM-12 (CAND-CAP-0011) shared claim rules: applicability/ref mapping and the governed-body freeze
/// comparison. Self-contained; ref values are never checked against a hardcoded vocabulary (sector-neutral), and
/// EvidenceRefs are stored opaque (no MOD-0031 contract is invented).</summary>
internal static class ClaimRules
{
    public static List<string> CleanRefs(IReadOnlyList<string>? refs)
        => (refs ?? Array.Empty<string>()).Select(r => r?.Trim() ?? string.Empty).Where(r => r.Length > 0).ToList();

    public static List<Guid> CleanGuidRefs(IReadOnlyList<Guid>? refs)
        => (refs ?? Array.Empty<Guid>()).Where(g => g != Guid.Empty).Distinct().ToList();

    public static ClaimApplicability ToDomain(ClaimApplicabilityInput? input)
        => new()
        {
            ProductRefs = CleanRefs(input?.ProductRefs),
            MarketRefs = CleanRefs(input?.MarketRefs),
            AudienceRefs = CleanRefs(input?.AudienceRefs),
            EligibilityPolicyId = input?.EligibilityPolicyId is { } id && id != Guid.Empty ? id : null
        };

    /// <summary>The governed body frozen on approval: text + qualifiers + applicability + evidence + component refs.</summary>
    public static bool BodyEqual(Claim entity, string claimText, List<string> qualifiers,
        ClaimApplicability applicability, List<string> evidenceRefs, List<Guid> componentRefs)
    {
        if (!string.Equals(entity.ClaimText, claimText, StringComparison.Ordinal)
            || !entity.Qualifiers.SequenceEqual(qualifiers)
            || !entity.EvidenceRefs.SequenceEqual(evidenceRefs)
            || !entity.ComponentRefs.SequenceEqual(componentRefs))
        {
            return false;
        }

        var a = entity.Applicability;
        return a.EligibilityPolicyId == applicability.EligibilityPolicyId
            && a.ProductRefs.SequenceEqual(applicability.ProductRefs)
            && a.MarketRefs.SequenceEqual(applicability.MarketRefs)
            && a.AudienceRefs.SequenceEqual(applicability.AudienceRefs);
    }
}

public sealed class CreateClaimHandler : IRequestHandler<CreateClaimCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IClaimRepository _claims;
    private readonly IContentCompositionAuditPublisher? _audit;

    public CreateClaimHandler(
        ITenantContext tenant, IActorContext actor, IClaimRepository claims,
        IContentCompositionAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _claims = claims;
        _audit = audit;
    }

    public async Task<Response<Guid>> Handle(CreateClaimCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.ClaimCode))
        {
            return Response<Guid>.Fail("ClaimCode is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.ClaimName))
        {
            return Response<Guid>.Fail("ClaimName is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.ClaimText))
        {
            return Response<Guid>.Fail("ClaimText is required.", 400);
        }

        // Create only lands a draft/inactive claim; approval is the dedicated ApproveClaimCommand (never on create).
        if (!string.IsNullOrWhiteSpace(request.Status)
            && (!ClaimStatuses.IsValid(request.Status)
                || string.Equals(request.Status.Trim(), ClaimStatuses.Approved, StringComparison.OrdinalIgnoreCase)
                || string.Equals(request.Status.Trim(), ClaimStatuses.Archived, StringComparison.OrdinalIgnoreCase)))
        {
            return Response<Guid>.Fail(
                $"Status on create must be one of: {ClaimStatuses.Draft}, {ClaimStatuses.Inactive} "
                + "(use the approve / archive endpoints for those transitions).", 400);
        }

        if (request.EffectiveTo is { } to && to < request.EffectiveFrom)
        {
            return Response<Guid>.Fail("EffectiveTo cannot be before EffectiveFrom.", 400);
        }

        var code = request.ClaimCode.Trim();
        if (await _claims.GetActiveByCodeAsync(tenantId, code, cancellationToken) is { } duplicate)
        {
            return Response<Guid>.Fail(
                $"A non-archived claim already uses ClaimCode '{code}' (claimId={duplicate.Id}).", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new Claim
        {
            TenantId = tenantId,
            ClaimCode = code,
            ClaimName = request.ClaimName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ClaimText = request.ClaimText.Trim(),
            Qualifiers = ClaimRules.CleanRefs(request.Qualifiers),
            Applicability = ClaimRules.ToDomain(request.Applicability),
            EvidenceRefs = ClaimRules.CleanRefs(request.EvidenceRefs),
            ComponentRefs = ClaimRules.CleanGuidRefs(request.ComponentRefs),
            ClaimVersion = string.IsNullOrWhiteSpace(request.ClaimVersion) ? "1.0" : request.ClaimVersion.Trim(),
            Status = ClaimStatuses.Normalize(request.Status),
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _claims.InsertAsync(entity, cancellationToken);
        if (_audit is not null)
        {
            await _audit.PublishAsync(ClaimReasonCodes.Created, tenantId,
                ContentCompositionAuditEntities.Claim, entity.Id, entity.Version, entity.ClaimCode, cancellationToken);
        }

        return Response<Guid>.Success(entity.Id, 201);
    }
}

public sealed class UpdateClaimHandler : IRequestHandler<UpdateClaimCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IClaimRepository _claims;
    private readonly IContentCompositionAuditPublisher? _audit;

    public UpdateClaimHandler(
        ITenantContext tenant, IActorContext actor, IClaimRepository claims,
        IContentCompositionAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _claims = claims;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(UpdateClaimCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _claims.GetByIdAsync(tenantId, request.ClaimId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("Claim not found.", 404);
        }

        if (entity.IsArchived())
        {
            return Response<bool>.Fail("An archived claim cannot be updated.", 409);
        }

        if (string.IsNullOrWhiteSpace(request.ClaimName))
        {
            return Response<bool>.Fail("ClaimName is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.ClaimText))
        {
            return Response<bool>.Fail("ClaimText is required.", 400);
        }

        // Status changes to approved/archived go through their own commands; update carries only draft/inactive.
        if (!string.IsNullOrWhiteSpace(request.Status)
            && (!ClaimStatuses.IsValid(request.Status)
                || string.Equals(request.Status.Trim(), ClaimStatuses.Approved, StringComparison.OrdinalIgnoreCase)
                || string.Equals(request.Status.Trim(), ClaimStatuses.Archived, StringComparison.OrdinalIgnoreCase)))
        {
            return Response<bool>.Fail(
                "Use the approve / archive endpoints for approved / archived transitions.", 400);
        }

        if (request.EffectiveTo is { } to && to < request.EffectiveFrom)
        {
            return Response<bool>.Fail("EffectiveTo cannot be before EffectiveFrom.", 400);
        }

        var qualifiers = ClaimRules.CleanRefs(request.Qualifiers);
        var applicability = ClaimRules.ToDomain(request.Applicability);
        var evidenceRefs = ClaimRules.CleanRefs(request.EvidenceRefs);
        var componentRefs = ClaimRules.CleanGuidRefs(request.ComponentRefs);
        var claimText = request.ClaimText.Trim();

        // An approved claim freezes its governed body — a change needs a new version.
        var bodyChanged = !ClaimRules.BodyEqual(entity, claimText, qualifiers, applicability, evidenceRefs, componentRefs);
        if (entity.IsApproved() && bodyChanged)
        {
            return Response<bool>.Fail(
                "The governed body is frozen on an approved claim; create a new version to change it.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        entity.ClaimName = request.ClaimName.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.ClaimText = claimText;
        entity.Qualifiers = qualifiers;
        entity.Applicability = applicability;
        entity.EvidenceRefs = evidenceRefs;
        entity.ComponentRefs = componentRefs;
        entity.Status = ClaimStatuses.Normalize(request.Status ?? entity.Status);
        if (!string.IsNullOrWhiteSpace(request.ClaimVersion))
        {
            entity.ClaimVersion = request.ClaimVersion.Trim();
        }

        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        entity.UpdatedAt = now;
        entity.UpdatedBy = _actor.ActorName;

        await _claims.UpdateAsync(entity, cancellationToken);
        if (_audit is not null)
        {
            await _audit.PublishAsync(ClaimReasonCodes.Updated, tenantId,
                ContentCompositionAuditEntities.Claim, entity.Id, entity.Version, entity.ClaimCode, cancellationToken);
        }

        return Response<bool>.Success(true);
    }
}

public sealed class ApproveClaimHandler : IRequestHandler<ApproveClaimCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IClaimRepository _claims;
    private readonly IContentCompositionAuditPublisher? _audit;

    public ApproveClaimHandler(
        ITenantContext tenant, IActorContext actor, IClaimRepository claims,
        IContentCompositionAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _claims = claims;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(ApproveClaimCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _claims.GetByIdAsync(tenantId, request.ClaimId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("Claim not found.", 404);
        }

        if (entity.IsArchived())
        {
            return Response<bool>.Fail("An archived claim cannot be approved.", 409);
        }

        if (entity.IsApproved())
        {
            return Response<bool>.Success(true); // idempotent — claim approval is not assembly approval
        }

        var now = DateTimeOffset.UtcNow;
        entity.Status = ClaimStatuses.Approved;
        entity.ApprovedAt = now;
        entity.ApprovedBy = _actor.ActorName;
        entity.UpdatedAt = now;
        entity.UpdatedBy = _actor.ActorName;

        await _claims.UpdateAsync(entity, cancellationToken);
        if (_audit is not null)
        {
            await _audit.PublishAsync(ClaimReasonCodes.Approved, tenantId,
                ContentCompositionAuditEntities.Claim, entity.Id, entity.Version, entity.ClaimCode, cancellationToken);
        }

        return Response<bool>.Success(true);
    }
}

public sealed class ArchiveClaimHandler : IRequestHandler<ArchiveClaimCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IClaimRepository _claims;
    private readonly IContentCompositionAuditPublisher? _audit;

    public ArchiveClaimHandler(
        ITenantContext tenant, IActorContext actor, IClaimRepository claims,
        IContentCompositionAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _claims = claims;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(ArchiveClaimCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _claims.GetByIdAsync(tenantId, request.ClaimId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("Claim not found.", 404);
        }

        if (entity.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        var now = DateTimeOffset.UtcNow;
        entity.Status = ClaimStatuses.Archived;
        entity.ArchivedAt = now;
        entity.ArchivedBy = _actor.ActorName;
        entity.UpdatedAt = now;
        entity.UpdatedBy = _actor.ActorName;

        await _claims.UpdateAsync(entity, cancellationToken);
        if (_audit is not null)
        {
            await _audit.PublishAsync(ClaimReasonCodes.Archived, tenantId,
                ContentCompositionAuditEntities.Claim, entity.Id, entity.Version, entity.ClaimCode, cancellationToken);
        }

        return Response<bool>.Success(true);
    }
}
