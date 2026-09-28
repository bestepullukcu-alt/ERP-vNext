using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

/// <summary>SCMM-12 (CAND-CAP-0011) shared claim rules: applicability/ref mapping and the governed-body freeze
/// comparison. Self-contained; ref values are never checked against a hardcoded vocabulary (sector-neutral), and
/// EvidenceRefs are stored opaque (no MOD-0031 contract is invented).</summary>
internal static class ClaimRules
{
    /// <summary>Create / update only land draft or inactive; approved / archived / in-review / review-required are
    /// lifecycle transitions with their own endpoints (or the WP-CL-BE-4 workflow).</summary>
    public static bool IsWritableStatus(string? status)
        => string.IsNullOrWhiteSpace(status)
           || status.Trim().ToLowerInvariant() is ClaimStatuses.Draft or ClaimStatuses.Inactive;

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
    private readonly IReferenceDataValidator? _references;
    private readonly IReferenceMetadataReader? _referenceMetadata;
    private readonly IStrategyTemplateProductReferenceValidator? _products;
    private readonly IAudienceProfileRepository? _audiences;

    public CreateClaimHandler(
        ITenantContext tenant, IActorContext actor, IClaimRepository claims,
        IContentCompositionAuditPublisher? audit = null,
        IReferenceDataValidator? references = null,
        IReferenceMetadataReader? referenceMetadata = null,
        IStrategyTemplateProductReferenceValidator? products = null,
        IAudienceProfileRepository? audiences = null)
    {
        _tenant = tenant;
        _actor = actor;
        _claims = claims;
        _audit = audit;
        _references = references;
        _referenceMetadata = referenceMetadata;
        _products = products;
        _audiences = audiences;
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
            && (!ClaimStatuses.IsValid(request.Status) || !ClaimRules.IsWritableStatus(request.Status)))
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

        // WP-CL-BE-1 — v2 fields (all optional for a pre-v2 client; Kind given ⇒ product required).
        var kindGiven = !string.IsNullOrWhiteSpace(request.Kind);
        if (kindGiven && !ClaimKinds.IsValid(request.Kind))
        {
            return new ClaimFailure(ClaimErrorCodes.InvalidKind,
                $"Kind must be one of: {string.Join(", ", ClaimKinds.All)}.").To<Guid>();
        }

        var kind = kindGiven ? request.Kind!.Trim().ToLowerInvariant() : ClaimKinds.Core;
        string? localCountry = null;
        string textLanguage;
        if (kind == ClaimKinds.Local)
        {
            localCountry = ClaimV2Checks.NormalizeCountry(request.LocalCountryCode);
            if (localCountry.Length == 0)
            {
                return new ClaimFailure(ClaimErrorCodes.LocalCountryRequired,
                    "A local claim needs LocalCountryCode.").To<Guid>();
            }

            if (await ClaimV2Checks.ValidateReferenceAsync(_references, ClaimReferenceSets.CountryCodes, localCountry,
                    "LocalCountryCode", cancellationToken) is { } countryFailure)
            {
                return countryFailure.To<Guid>();
            }

            var (languages, languageFailure) =
                await ClaimV2Checks.GetCountryLanguagesAsync(_referenceMetadata, localCountry, cancellationToken);
            if (languageFailure is not null)
            {
                return languageFailure.To<Guid>();
            }

            textLanguage = string.IsNullOrWhiteSpace(request.TextLanguageCode)
                ? languages[0]
                : ClaimV2Checks.NormalizeLanguage(request.TextLanguageCode);
            if (!languages.Contains(textLanguage))
            {
                return new ClaimFailure(ClaimErrorCodes.LanguageNotAllowed,
                    $"TextLanguageCode '{textLanguage}' is not a content language of '{localCountry}'.").To<Guid>();
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.LocalCountryCode))
            {
                return new ClaimFailure(ClaimErrorCodes.NotApplicable,
                    "LocalCountryCode applies only to a local claim.").To<Guid>();
            }

            textLanguage = string.IsNullOrWhiteSpace(request.TextLanguageCode)
                ? ClaimReferenceSets.DefaultCoreLanguage
                : ClaimV2Checks.NormalizeLanguage(request.TextLanguageCode);
        }

        var productId = request.ProductId is { } pid && pid != Guid.Empty ? pid : (Guid?)null;
        if (kindGiven && productId is null)
        {
            return new ClaimFailure(ClaimErrorCodes.ProductRequired, "ProductId is required.").To<Guid>();
        }

        if (productId is { } p
            && await ClaimV2Checks.ValidateProductAsync(_products, p, cancellationToken) is { } productFailure)
        {
            return productFailure.To<Guid>();
        }

        var audienceIds = ClaimRules.CleanGuidRefs(request.AudienceProfileIds);
        if (await ClaimV2Checks.ValidateAudiencesAsync(_audiences, tenantId, audienceIds, cancellationToken)
            is { } audienceFailure)
        {
            return audienceFailure.To<Guid>();
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new Claim
        {
            Kind = kind,
            LocalCountryCode = localCountry,
            ProductId = productId,
            ProductDisplay = productId is null || string.IsNullOrWhiteSpace(request.ProductDisplay)
                ? null
                : request.ProductDisplay.Trim(),
            AudienceProfileIds = audienceIds,
            ResponsibleOrgUnitId = request.ResponsibleOrgUnitId is { } org && org != Guid.Empty ? org : null,
            TextLanguageCode = textLanguage,
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
    private readonly IReferenceMetadataReader? _referenceMetadata;
    private readonly IStrategyTemplateProductReferenceValidator? _products;
    private readonly IAudienceProfileRepository? _audiences;

    public UpdateClaimHandler(
        ITenantContext tenant, IActorContext actor, IClaimRepository claims,
        IContentCompositionAuditPublisher? audit = null,
        IReferenceMetadataReader? referenceMetadata = null,
        IStrategyTemplateProductReferenceValidator? products = null,
        IAudienceProfileRepository? audiences = null)
    {
        _tenant = tenant;
        _actor = actor;
        _claims = claims;
        _audit = audit;
        _referenceMetadata = referenceMetadata;
        _products = products;
        _audiences = audiences;
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

        // WP-CL-BE-1 — a record under review is locked until the review closes (WP-CL-BE-4 workflow).
        if (entity.Status == ClaimStatuses.InReview)
        {
            return new ClaimFailure(ClaimErrorCodes.InvalidState,
                "A claim under review cannot be updated.", 409).To<bool>();
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
            && (!ClaimStatuses.IsValid(request.Status) || !ClaimRules.IsWritableStatus(request.Status)))
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

        // WP-CL-BE-1 — v2 members: null keeps the stored value (a pre-v2 client never wipes them).
        var productId = request.ProductId is { } pid && pid != Guid.Empty ? pid : entity.ProductId;
        var audienceIds = request.AudienceProfileIds is null
            ? entity.AudienceProfileIds.ToList()
            : ClaimRules.CleanGuidRefs(request.AudienceProfileIds);
        var textLanguage = string.IsNullOrWhiteSpace(request.TextLanguageCode)
            ? entity.TextLanguageCode
            : ClaimV2Checks.NormalizeLanguage(request.TextLanguageCode);
        var v2BodyChanged = productId != entity.ProductId
            || !audienceIds.SequenceEqual(entity.AudienceProfileIds)
            || !string.Equals(textLanguage, entity.TextLanguageCode, StringComparison.Ordinal);

        // An approved claim freezes its governed body — a change needs a new version.
        var bodyChanged = !ClaimRules.BodyEqual(entity, claimText, qualifiers, applicability, evidenceRefs, componentRefs);
        if (entity.IsApproved() && (bodyChanged || v2BodyChanged))
        {
            return Response<bool>.Fail(
                "The governed body is frozen on an approved claim; create a new version to change it.", 409);
        }

        if (productId != entity.ProductId && productId is { } p
            && await ClaimV2Checks.ValidateProductAsync(_products, p, cancellationToken) is { } productFailure)
        {
            return productFailure.To<bool>();
        }

        if (!audienceIds.SequenceEqual(entity.AudienceProfileIds)
            && await ClaimV2Checks.ValidateAudiencesAsync(_audiences, tenantId, audienceIds, cancellationToken)
                is { } audienceFailure)
        {
            return audienceFailure.To<bool>();
        }

        if (entity.IsLocal() && textLanguage is not null
            && !string.Equals(textLanguage, entity.TextLanguageCode, StringComparison.Ordinal))
        {
            var (languages, languageFailure) = await ClaimV2Checks.GetCountryLanguagesAsync(
                _referenceMetadata, entity.LocalCountryCode ?? string.Empty, cancellationToken);
            if (languageFailure is not null)
            {
                return languageFailure.To<bool>();
            }

            if (!languages.Contains(textLanguage))
            {
                return new ClaimFailure(ClaimErrorCodes.LanguageNotAllowed,
                    $"TextLanguageCode '{textLanguage}' is not a content language of '{entity.LocalCountryCode}'.")
                    .To<bool>();
            }
        }

        var now = DateTimeOffset.UtcNow;
        entity.ProductId = productId;
        if (request.ProductDisplay is not null)
        {
            entity.ProductDisplay = string.IsNullOrWhiteSpace(request.ProductDisplay) ? null : request.ProductDisplay.Trim();
        }

        entity.AudienceProfileIds = audienceIds;
        if (request.ResponsibleOrgUnitId is { } org)
        {
            entity.ResponsibleOrgUnitId = org == Guid.Empty ? null : org;
        }

        entity.TextLanguageCode = textLanguage;
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

    private readonly IClaimCountryVersionRepository? _countryVersions;

    public ApproveClaimHandler(
        ITenantContext tenant, IActorContext actor, IClaimRepository claims,
        IContentCompositionAuditPublisher? audit = null,
        IClaimCountryVersionRepository? countryVersions = null)
    {
        _tenant = tenant;
        _actor = actor;
        _claims = claims;
        _audit = audit;
        _countryVersions = countryVersions;
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

        await PropagateAsync(tenantId, entity, now, cancellationToken);
        return Response<bool>.Success(true);
    }

    /// <summary>
    /// WP-CL-BE-1 — core approval propagation (here on the direct approve; WP-CL-BE-4 moves it to the workflow
    /// outcome): every OTHER approved record of the ClaimCode becomes <c>inactive</c>; when one was superseded, the
    /// code's approved country versions bound to an older core become <c>review-required</c> (ApprovedAt kept — the
    /// usability decision is the content side's).
    /// </summary>
    private async Task PropagateAsync(Guid tenantId, Claim approved, DateTimeOffset now, CancellationToken ct)
    {
        var superseded = 0;
        foreach (var other in await _claims.ListByCodeAsync(tenantId, approved.ClaimCode, ct))
        {
            if (other.Id == approved.Id || !other.IsApproved() || other.IsArchived())
            {
                continue;
            }

            other.Status = ClaimStatuses.Inactive;
            other.UpdatedAt = now;
            other.UpdatedBy = _actor.ActorName;
            await _claims.UpdateAsync(other, ct);
            superseded++;
        }

        if ((superseded == 0 && approved.SupersedesClaimId is null) || _countryVersions is null)
        {
            return;
        }

        var flagged = 0;
        foreach (var version in await _countryVersions.ListByClaimCodeAsync(tenantId, approved.ClaimCode, ct))
        {
            if (version.Status != ClaimStatuses.Approved || version.IsArchived()
                || string.Equals(version.BoundCoreVersion, approved.ClaimVersion, StringComparison.Ordinal))
            {
                continue;
            }

            version.Status = ClaimStatuses.ReviewRequired;
            version.UpdatedAt = now;
            version.UpdatedBy = _actor.ActorName;
            await _countryVersions.UpdateAsync(version, ct);
            flagged++;
        }

        if (flagged > 0 && _audit is not null)
        {
            await _audit.PublishAsync(ClaimReasonCodes.CountryVersionsReviewRequired, tenantId,
                ContentCompositionAuditEntities.Claim, approved.Id, approved.Version,
                $"{approved.ClaimCode}|count={flagged}", ct);
        }
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
