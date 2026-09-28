using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

// WP-CL-BE-1 (claims v2) — core new version, claim × country closures, claim country versions. Every rule is
// fail-closed with a stable error code (ClaimErrorCodes); country / language / reason / adaptation values are read
// from MOD-0048 reference sets, never compiled in. No workflow (WP-CL-BE-4), no evidence rule (WP-CL-BE-5), no
// country-level permission (D1). Audit events carry ids, codes and countries only — never wording.

/// <summary>Opens the next core major from an approved claim.</summary>
public sealed class CreateClaimNewVersionHandler : IRequestHandler<CreateClaimNewVersionCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IClaimRepository _claims;
    private readonly IContentCompositionAuditPublisher? _audit;
    private readonly IClaimEvidenceClient? _evidence;
    private readonly ILogger<CreateClaimNewVersionHandler>? _logger;

    public CreateClaimNewVersionHandler(
        ITenantContext tenant, IActorContext actor, IClaimRepository claims,
        IContentCompositionAuditPublisher? audit = null, IClaimEvidenceClient? evidence = null,
        ILogger<CreateClaimNewVersionHandler>? logger = null)
    {
        _evidence = evidence;
        _logger = logger;
        _tenant = tenant;
        _actor = actor;
        _claims = claims;
        _audit = audit;
    }

    public async Task<Response<Guid>> Handle(CreateClaimNewVersionCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var source = await _claims.GetByIdAsync(tenantId, request.ClaimId, cancellationToken);
        if (source is null)
        {
            return Response<Guid>.Fail("Claim not found.", 404);
        }

        if (source.Status == ClaimStatuses.InReview)
        {
            return ClaimReviewRules.InReviewLocked<Guid>();
        }

        // WP-CL-BE-5 — a record turned review-required by a changed evidence document is fixed by a new version too.
        if (source.IsArchived() || !(source.IsApproved() || source.Status == ClaimStatuses.ReviewRequired))
        {
            return new ClaimFailure(ClaimErrorCodes.InvalidState,
                "A new version can only be opened from an approved or review-required claim.", 409).To<Guid>();
        }

        var records = await _claims.ListByCodeAsync(tenantId, source.ClaimCode, cancellationToken);
        if (records.FirstOrDefault(r => !r.IsArchived()
                && r.Status is ClaimStatuses.Draft or ClaimStatuses.InReview) is { } open)
        {
            return new ClaimFailure(ClaimErrorCodes.OpenVersionExists,
                $"ClaimCode '{source.ClaimCode}' already has an open {open.Status} version (claimId={open.Id}).", 409)
                .To<Guid>();
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new Claim
        {
            TenantId = tenantId,
            ClaimCode = source.ClaimCode,
            ClaimName = source.ClaimName,
            Description = source.Description,
            ClaimText = source.ClaimText,
            Qualifiers = source.Qualifiers.ToList(),
            Applicability = new ClaimApplicability
            {
                ProductRefs = source.Applicability.ProductRefs.ToList(),
                MarketRefs = source.Applicability.MarketRefs.ToList(),
                AudienceRefs = source.Applicability.AudienceRefs.ToList(),
                EligibilityPolicyId = source.Applicability.EligibilityPolicyId
            },
            EvidenceRefs = source.EvidenceRefs.ToList(),
            ComponentRefs = source.ComponentRefs.ToList(),
            // Major +1 over every record of the code; legacy free-text versions that do not parse ⇒ "2.0".
            ClaimVersion = ClaimVersioning.NextMajor(records.Select(r => r.ClaimVersion)),
            Status = ClaimStatuses.Draft,
            EffectiveFrom = source.EffectiveFrom,
            EffectiveTo = source.EffectiveTo,
            Kind = string.IsNullOrWhiteSpace(source.Kind) ? ClaimKinds.Core : source.Kind,
            LocalCountryCode = source.LocalCountryCode,
            ProductId = source.ProductId,
            ProductDisplay = source.ProductDisplay,
            AudienceProfileIds = source.AudienceProfileIds.ToList(),
            ResponsibleOrgUnitId = source.ResponsibleOrgUnitId,
            TextLanguageCode = source.TextLanguageCode,
            SupersedesClaimId = source.Id,
            // Closures are per ClaimCode: the history travels with the line.
            CountryClosures = source.CountryClosures.Select(ClaimClosureOps.Copy).ToList(),
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _claims.InsertAsync(entity, cancellationToken);
        if (_audit is not null)
        {
            await _audit.PublishAsync(ClaimReasonCodes.NewVersionCreated, tenantId,
                ContentCompositionAuditEntities.Claim, entity.Id, entity.Version,
                $"{entity.ClaimCode}|{entity.ClaimVersion}", cancellationToken);
        }

        // WP-CL-BE-5 — the source record's OWN active evidence is linked again under the new ObjectRef; a failed copy
        // never fails the new version (the submit rule catches a version left without evidence).
        await ClaimEvidenceCopy.CopyAsync(_evidence, _audit, _logger, tenantId, ClaimEvidenceRules.For(source),
            ClaimEvidenceRules.For(entity), ContentCompositionAuditEntities.Claim, entity.Id, entity.Version,
            entity.ClaimCode, cancellationToken);

        return Response<Guid>.Success(entity.Id, 201);
    }
}

internal static class ClaimClosureOps
{
    public static ClaimCountryClosure Copy(ClaimCountryClosure c) => new()
    {
        CountryCode = c.CountryCode,
        ReasonCode = c.ReasonCode,
        ClosedBy = c.ClosedBy,
        ClosedAt = c.ClosedAt,
        ReopenedBy = c.ReopenedBy,
        ReopenedAt = c.ReopenedAt,
        ReopenNote = c.ReopenNote
    };

    /// <summary>The records a closure change is mirrored onto: every live record of the code (plus the addressed one).</summary>
    public static IEnumerable<Claim> Targets(IEnumerable<Claim> records, Claim addressed)
        => records.Where(r => r.Id == addressed.Id || ClaimLines.IsLive(r))
            .Where(r => !r.IsArchived())
            .GroupBy(r => r.Id).Select(g => g.First());
}

/// <summary>Closes a claim × country cell ("will not be opened" + single-choice reason).</summary>
public sealed class CloseClaimCountryHandler : IRequestHandler<CloseClaimCountryCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IClaimRepository _claims;
    private readonly IClaimCountryVersionRepository _countryVersions;
    private readonly IReferenceDataValidator? _references;
    private readonly IContentCompositionAuditPublisher? _audit;

    public CloseClaimCountryHandler(
        ITenantContext tenant, IActorContext actor, IClaimRepository claims,
        IClaimCountryVersionRepository countryVersions, IReferenceDataValidator? references = null,
        IContentCompositionAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _claims = claims;
        _countryVersions = countryVersions;
        _references = references;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(CloseClaimCountryCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var claim = await _claims.GetByIdAsync(tenantId, request.ClaimId, cancellationToken);
        if (claim is null)
        {
            return Response<bool>.Fail("Claim not found.", 404);
        }

        if (claim.IsArchived())
        {
            return new ClaimFailure(ClaimErrorCodes.InvalidState, "An archived claim cannot be changed.", 409).To<bool>();
        }

        var country = ClaimV2Checks.NormalizeCountry(request.CountryCode);
        if (country.Length == 0)
        {
            return new ClaimFailure(ClaimErrorCodes.Required, "CountryCode is required.").To<bool>();
        }

        if (claim.IsLocal() && !string.Equals(country, claim.LocalCountryCode, StringComparison.OrdinalIgnoreCase))
        {
            return new ClaimFailure(ClaimErrorCodes.NotApplicable,
                $"A local claim of '{claim.LocalCountryCode}' has no '{country}' cell.").To<bool>();
        }

        if (await ClaimV2Checks.ValidateReferenceAsync(_references, ClaimReferenceSets.CountryCodes, country,
                "CountryCode", cancellationToken) is { } countryFailure)
        {
            return countryFailure.To<bool>();
        }

        var reason = (request.ReasonCode ?? string.Empty).Trim();
        if (await ClaimV2Checks.ValidateReferenceAsync(_references, ClaimReferenceSets.ClosureReason, reason,
                "ReasonCode", cancellationToken) is { } reasonFailure)
        {
            return reasonFailure.To<bool>();
        }

        var versions = await _countryVersions.ListByClaimCodeAsync(tenantId, claim.ClaimCode, cancellationToken);
        if (versions.FirstOrDefault(v => v.IsLive()
                && string.Equals(v.CountryCode, country, StringComparison.OrdinalIgnoreCase)) is { } live)
        {
            return new ClaimFailure(ClaimErrorCodes.CountryHasVersion,
                $"'{country}' has a {live.Status} country version (id={live.Id}); archive it before closing.", 409)
                .To<bool>();
        }

        var records = await _claims.ListByCodeAsync(tenantId, claim.ClaimCode, cancellationToken);
        var current = ClaimLines.Current(records) ?? claim;
        if (current.ActiveClosureFor(country) is not null || claim.ActiveClosureFor(country) is not null)
        {
            return new ClaimFailure(ClaimErrorCodes.CountryAlreadyClosed, $"'{country}' is already closed.", 409)
                .To<bool>();
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var target in ClaimClosureOps.Targets(records, claim))
        {
            target.CountryClosures.Add(new ClaimCountryClosure
            {
                CountryCode = country,
                ReasonCode = reason,
                ClosedBy = _actor.ActorName,
                ClosedAt = now
            });
            target.UpdatedAt = now;
            target.UpdatedBy = _actor.ActorName;
            await _claims.UpdateAsync(target, cancellationToken);
        }

        if (_audit is not null)
        {
            await _audit.PublishAsync(ClaimReasonCodes.CountryClosed, tenantId,
                ContentCompositionAuditEntities.Claim, claim.Id, claim.Version,
                $"{claim.ClaimCode}|{country}|{reason}", cancellationToken);
        }

        return Response<bool>.Success(true);
    }
}

/// <summary>Reopens a closed claim × country cell (stamps the closure; history is never removed).</summary>
public sealed class ReopenClaimCountryHandler : IRequestHandler<ReopenClaimCountryCommand, Response<bool>>
{
    private const int MaxNoteLength = 1000;

    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IClaimRepository _claims;
    private readonly IContentCompositionAuditPublisher? _audit;

    public ReopenClaimCountryHandler(
        ITenantContext tenant, IActorContext actor, IClaimRepository claims,
        IContentCompositionAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _claims = claims;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(ReopenClaimCountryCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var claim = await _claims.GetByIdAsync(tenantId, request.ClaimId, cancellationToken);
        if (claim is null)
        {
            return Response<bool>.Fail("Claim not found.", 404);
        }

        if (claim.IsArchived())
        {
            return new ClaimFailure(ClaimErrorCodes.InvalidState, "An archived claim cannot be changed.", 409).To<bool>();
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > MaxNoteLength })
        {
            return new ClaimFailure(ClaimErrorCodes.Required,
                $"Note cannot exceed {MaxNoteLength} characters.").To<bool>();
        }

        var country = ClaimV2Checks.NormalizeCountry(request.CountryCode);
        var records = await _claims.ListByCodeAsync(tenantId, claim.ClaimCode, cancellationToken);
        var targets = ClaimClosureOps.Targets(records, claim)
            .Where(r => r.ActiveClosureFor(country) is not null).ToList();
        if (targets.Count == 0)
        {
            return new ClaimFailure(ClaimErrorCodes.CountryNotClosed, $"'{country}' is not closed.", 409).To<bool>();
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var target in targets)
        {
            var closure = target.ActiveClosureFor(country)!;
            closure.ReopenedAt = now;
            closure.ReopenedBy = _actor.ActorName;
            closure.ReopenNote = note;
            target.UpdatedAt = now;
            target.UpdatedBy = _actor.ActorName;
            await _claims.UpdateAsync(target, cancellationToken);
        }

        if (_audit is not null)
        {
            await _audit.PublishAsync(ClaimReasonCodes.CountryReopened, tenantId,
                ContentCompositionAuditEntities.Claim, claim.Id, claim.Version,
                $"{claim.ClaimCode}|{country}", cancellationToken);
        }

        return Response<bool>.Success(true);
    }
}

/// <summary>The content validation shared by country-version create / update.</summary>
internal sealed class ClaimCountryVersionContent
{
    public List<ClaimLocalizedText> Texts { get; init; } = new();
    public List<ClaimLocalizedText> Qualifiers { get; init; } = new();
    public string AdaptationTypeCode { get; init; } = string.Empty;
    public string? AdaptationReason { get; init; }
    public List<Guid> AudienceProfileIds { get; init; } = new();

    public static async Task<(ClaimCountryVersionContent? Content, ClaimFailure? Failure)> ValidateAsync(
        Guid tenantId,
        Claim boundClaim,
        string country,
        IReadOnlyList<ClaimLocalizedTextInput>? texts,
        IReadOnlyList<ClaimLocalizedTextInput>? qualifiers,
        string? adaptationTypeCode,
        string? adaptationReason,
        IReadOnlyList<Guid>? audienceProfileIds,
        DateTimeOffset validFrom,
        DateTimeOffset? validTo,
        IReferenceDataValidator? references,
        IReferenceMetadataReader? metadata,
        IAudienceProfileRepository? audiences,
        CancellationToken ct)
    {
        if (validFrom == default)
        {
            return (null, new ClaimFailure(ClaimErrorCodes.Required, "ValidFrom is required."));
        }

        // Date-level comparison (CrmService DateTimeOffset pitfall: same calendar day at another offset is valid).
        if (validTo is { } to && to.Date < validFrom.Date)
        {
            return (null, new ClaimFailure(ClaimErrorCodes.InvalidValidity, "ValidTo cannot be before ValidFrom."));
        }

        var (cleanTexts, textFailure) = ClaimV2Checks.CleanTexts(texts, "Texts");
        if (textFailure is not null)
        {
            return (null, textFailure);
        }

        if (cleanTexts.Count == 0)
        {
            return (null, new ClaimFailure(ClaimErrorCodes.TextsRequired, "At least one language text is required."));
        }

        var (cleanQualifiers, qualifierFailure) = ClaimV2Checks.CleanTexts(qualifiers, "Qualifiers");
        if (qualifierFailure is not null)
        {
            return (null, qualifierFailure);
        }

        var (languages, languageFailure) = await ClaimV2Checks.GetCountryLanguagesAsync(metadata, country, ct);
        if (languageFailure is not null)
        {
            return (null, languageFailure);
        }

        if (cleanTexts.Concat(cleanQualifiers).FirstOrDefault(t => !languages.Contains(t.LanguageCode)) is { } bad)
        {
            return (null, new ClaimFailure(ClaimErrorCodes.LanguageNotAllowed,
                $"'{bad.LanguageCode}' is not a content language of '{country}' ({string.Join(", ", languages)})."));
        }

        var adaptation = (adaptationTypeCode ?? string.Empty).Trim().ToLowerInvariant();
        if (await ClaimV2Checks.ValidateReferenceAsync(references, ClaimReferenceSets.AdaptationType, adaptation,
                "AdaptationTypeCode", ct) is { } adaptationFailure)
        {
            return (null, adaptationFailure);
        }

        var reason = string.IsNullOrWhiteSpace(adaptationReason) ? null : adaptationReason.Trim();
        if (adaptation != ClaimReferenceSets.VerbatimAdaptation && reason is null)
        {
            return (null, new ClaimFailure(ClaimErrorCodes.AdaptationReasonRequired,
                $"AdaptationReason is required for a '{adaptation}' adaptation."));
        }

        var audienceIds = ClaimRules.CleanGuidRefs(audienceProfileIds);
        if (boundClaim.AudienceProfileIds.Count > 0
            && audienceIds.FirstOrDefault(id => !boundClaim.AudienceProfileIds.Contains(id)) is var extra
            && extra != Guid.Empty)
        {
            return (null, new ClaimFailure(ClaimErrorCodes.AudienceNotNarrowing,
                $"Audience profile '{extra}' is not an audience of the claim; a country version can only narrow it."));
        }

        if (await ClaimV2Checks.ValidateAudiencesAsync(audiences, tenantId, audienceIds, ct) is { } audienceFailure)
        {
            return (null, audienceFailure);
        }

        return (new ClaimCountryVersionContent
        {
            Texts = cleanTexts,
            Qualifiers = cleanQualifiers,
            AdaptationTypeCode = adaptation,
            AdaptationReason = reason,
            AudienceProfileIds = audienceIds
        }, null);
    }
}

/// <summary>Opens the first country version of a claim for one country.</summary>
public sealed class CreateClaimCountryVersionHandler
    : IRequestHandler<CreateClaimCountryVersionCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IClaimRepository _claims;
    private readonly IClaimCountryVersionRepository _countryVersions;
    private readonly IReferenceDataValidator? _references;
    private readonly IReferenceMetadataReader? _metadata;
    private readonly IAudienceProfileRepository? _audiences;
    private readonly IContentCompositionAuditPublisher? _audit;

    public CreateClaimCountryVersionHandler(
        ITenantContext tenant, IActorContext actor, IClaimRepository claims,
        IClaimCountryVersionRepository countryVersions,
        IReferenceDataValidator? references = null, IReferenceMetadataReader? metadata = null,
        IAudienceProfileRepository? audiences = null, IContentCompositionAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _claims = claims;
        _countryVersions = countryVersions;
        _references = references;
        _metadata = metadata;
        _audiences = audiences;
        _audit = audit;
    }

    public async Task<Response<Guid>> Handle(CreateClaimCountryVersionCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var claim = await _claims.GetByIdAsync(tenantId, request.ClaimId, cancellationToken);
        if (claim is null)
        {
            return Response<Guid>.Fail("Claim not found.", 404);
        }

        if (claim.IsArchived())
        {
            return new ClaimFailure(ClaimErrorCodes.InvalidState, "An archived claim cannot be localised.", 409)
                .To<Guid>();
        }

        var country = ClaimV2Checks.NormalizeCountry(request.CountryCode);
        if (country.Length == 0)
        {
            return new ClaimFailure(ClaimErrorCodes.Required, "CountryCode is required.").To<Guid>();
        }

        if (claim.IsLocal())
        {
            if (!string.Equals(country, claim.LocalCountryCode, StringComparison.OrdinalIgnoreCase))
            {
                return new ClaimFailure(ClaimErrorCodes.NotApplicable,
                    $"A local claim of '{claim.LocalCountryCode}' has no '{country}' version.").To<Guid>();
            }
        }
        else if (!claim.IsApproved())
        {
            return new ClaimFailure(ClaimErrorCodes.CoreNotApproved,
                "The core claim must be approved before a country version is opened.", 409).To<Guid>();
        }

        if (await ClaimV2Checks.ValidateReferenceAsync(_references, ClaimReferenceSets.CountryCodes, country,
                "CountryCode", cancellationToken) is { } countryFailure)
        {
            return countryFailure.To<Guid>();
        }

        var records = await _claims.ListByCodeAsync(tenantId, claim.ClaimCode, cancellationToken);
        var current = ClaimLines.Current(records) ?? claim;
        if (current.ActiveClosureFor(country) is not null || claim.ActiveClosureFor(country) is not null)
        {
            return new ClaimFailure(ClaimErrorCodes.CountryClosed,
                $"'{country}' is closed for this claim; reopen it first.", 409).To<Guid>();
        }

        var siblings = (await _countryVersions.ListByClaimCodeAsync(tenantId, claim.ClaimCode, cancellationToken))
            .Where(v => string.Equals(v.CountryCode, country, StringComparison.OrdinalIgnoreCase)).ToList();
        if (siblings.FirstOrDefault(v => v.IsLive()) is { } existing)
        {
            return new ClaimFailure(ClaimErrorCodes.CountryVersionExists,
                $"'{country}' already has a {existing.Status} version (id={existing.Id}); use new-version.", 409)
                .To<Guid>();
        }

        var (content, failure) = await ClaimCountryVersionContent.ValidateAsync(
            tenantId, claim, country, request.Texts, request.Qualifiers, request.AdaptationTypeCode,
            request.AdaptationReason, request.AudienceProfileIds, request.ValidFrom, request.ValidTo,
            _references, _metadata, _audiences, cancellationToken);
        if (failure is not null)
        {
            return failure.To<Guid>();
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new ClaimCountryVersion
        {
            TenantId = tenantId,
            ClaimCode = claim.ClaimCode,
            ClaimId = claim.Id,
            BoundCoreVersion = claim.ClaimVersion,
            CountryCode = country,
            // History (archived / inactive) keeps its numbers — a reopened line continues above them.
            CountryVersion = siblings.Count == 0
                ? "1.0"
                : ClaimVersioning.NextMinor("1.0", siblings.Select(s => s.CountryVersion)),
            Texts = content!.Texts,
            Qualifiers = content.Qualifiers,
            AdaptationTypeCode = content.AdaptationTypeCode,
            AdaptationReason = content.AdaptationReason,
            AudienceProfileIds = content.AudienceProfileIds,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            Status = ClaimStatuses.Draft,
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _countryVersions.InsertAsync(entity, cancellationToken);
        await ClaimCountryVersionAudit.PublishAsync(_audit, ClaimReasonCodes.CountryVersionCreated, tenantId, entity,
            cancellationToken);
        return Response<Guid>.Success(entity.Id, 201);
    }
}

internal static class ClaimCountryVersionAudit
{
    public static Task PublishAsync(IContentCompositionAuditPublisher? audit, string eventName, Guid tenantId,
        ClaimCountryVersion v, CancellationToken ct)
        => audit is null
            ? Task.CompletedTask
            : audit.PublishAsync(eventName, tenantId, ContentCompositionAuditEntities.ClaimCountryVersion, v.Id,
                v.Version, $"{v.ClaimCode}|{v.CountryCode}|{v.CountryVersion}", ct);
}

/// <summary>Full replace of a DRAFT country version's content.</summary>
public sealed class UpdateClaimCountryVersionHandler
    : IRequestHandler<UpdateClaimCountryVersionCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IClaimRepository _claims;
    private readonly IClaimCountryVersionRepository _countryVersions;
    private readonly IReferenceDataValidator? _references;
    private readonly IReferenceMetadataReader? _metadata;
    private readonly IAudienceProfileRepository? _audiences;
    private readonly IContentCompositionAuditPublisher? _audit;

    public UpdateClaimCountryVersionHandler(
        ITenantContext tenant, IActorContext actor, IClaimRepository claims,
        IClaimCountryVersionRepository countryVersions,
        IReferenceDataValidator? references = null, IReferenceMetadataReader? metadata = null,
        IAudienceProfileRepository? audiences = null, IContentCompositionAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _claims = claims;
        _countryVersions = countryVersions;
        _references = references;
        _metadata = metadata;
        _audiences = audiences;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(UpdateClaimCountryVersionCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var version = await _countryVersions.GetByIdAsync(tenantId, request.CountryVersionId, cancellationToken);
        if (version is null)
        {
            return Response<bool>.Fail("Country version not found.", 404);
        }

        if (version.Status == ClaimStatuses.InReview)
        {
            return ClaimReviewRules.InReviewLocked<bool>();
        }

        if (version.Status != ClaimStatuses.Draft || version.IsArchived())
        {
            return new ClaimFailure(ClaimErrorCodes.VersionLocked,
                $"A {version.Status} country version is locked; open a new version to change it.", 409).To<bool>();
        }

        var claim = await _claims.GetByIdAsync(tenantId, version.ClaimId, cancellationToken);
        if (claim is null)
        {
            return Response<bool>.Fail("Bound claim not found.", 404);
        }

        var (content, failure) = await ClaimCountryVersionContent.ValidateAsync(
            tenantId, claim, version.CountryCode, request.Texts, request.Qualifiers, request.AdaptationTypeCode,
            request.AdaptationReason, request.AudienceProfileIds, request.ValidFrom, request.ValidTo,
            _references, _metadata, _audiences, cancellationToken);
        if (failure is not null)
        {
            return failure.To<bool>();
        }

        version.Texts = content!.Texts;
        version.Qualifiers = content.Qualifiers;
        version.AdaptationTypeCode = content.AdaptationTypeCode;
        version.AdaptationReason = content.AdaptationReason;
        version.AudienceProfileIds = content.AudienceProfileIds;
        version.ValidFrom = request.ValidFrom;
        version.ValidTo = request.ValidTo;
        version.UpdatedAt = DateTimeOffset.UtcNow;
        version.UpdatedBy = _actor.ActorName;

        await _countryVersions.UpdateAsync(version, cancellationToken);
        await ClaimCountryVersionAudit.PublishAsync(_audit, ClaimReasonCodes.CountryVersionUpdated, tenantId, version,
            cancellationToken);
        return Response<bool>.Success(true);
    }
}

/// <summary>Opens the next country minor from an approved / review-required version, bound to the latest approved
/// core record of the claim code.</summary>
public sealed class CreateClaimCountryNewVersionHandler
    : IRequestHandler<CreateClaimCountryNewVersionCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IClaimRepository _claims;
    private readonly IClaimCountryVersionRepository _countryVersions;
    private readonly IContentCompositionAuditPublisher? _audit;
    private readonly IClaimEvidenceClient? _evidence;
    private readonly ILogger<CreateClaimCountryNewVersionHandler>? _logger;

    public CreateClaimCountryNewVersionHandler(
        ITenantContext tenant, IActorContext actor, IClaimRepository claims,
        IClaimCountryVersionRepository countryVersions, IContentCompositionAuditPublisher? audit = null,
        IClaimEvidenceClient? evidence = null, ILogger<CreateClaimCountryNewVersionHandler>? logger = null)
    {
        _evidence = evidence;
        _logger = logger;
        _tenant = tenant;
        _actor = actor;
        _claims = claims;
        _countryVersions = countryVersions;
        _audit = audit;
    }

    public async Task<Response<Guid>> Handle(CreateClaimCountryNewVersionCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var source = await _countryVersions.GetByIdAsync(tenantId, request.CountryVersionId, cancellationToken);
        if (source is null)
        {
            return Response<Guid>.Fail("Country version not found.", 404);
        }

        if (source.Status == ClaimStatuses.InReview)
        {
            return ClaimReviewRules.InReviewLocked<Guid>();
        }

        if (source.IsArchived() || source.Status is not (ClaimStatuses.Approved or ClaimStatuses.ReviewRequired))
        {
            return new ClaimFailure(ClaimErrorCodes.InvalidState,
                "A new country version can only be opened from an approved or review-required version.", 409)
                .To<Guid>();
        }

        var siblings = (await _countryVersions.ListByClaimCodeAsync(tenantId, source.ClaimCode, cancellationToken))
            .Where(v => string.Equals(v.CountryCode, source.CountryCode, StringComparison.OrdinalIgnoreCase)).ToList();
        if (siblings.FirstOrDefault(v => !v.IsArchived() && v.IsOpen()) is { } open)
        {
            return new ClaimFailure(ClaimErrorCodes.OpenVersionExists,
                $"'{source.CountryCode}' already has an open {open.Status} version (id={open.Id}).", 409).To<Guid>();
        }

        // Bind to the latest APPROVED core (or local) record of the code; none ⇒ keep the source binding.
        var latest = ClaimLines.LatestApproved(
            await _claims.ListByCodeAsync(tenantId, source.ClaimCode, cancellationToken));

        var now = DateTimeOffset.UtcNow;
        var entity = new ClaimCountryVersion
        {
            TenantId = tenantId,
            ClaimCode = source.ClaimCode,
            ClaimId = latest?.Id ?? source.ClaimId,
            BoundCoreVersion = latest?.ClaimVersion ?? source.BoundCoreVersion,
            CountryCode = source.CountryCode,
            CountryVersion = ClaimVersioning.NextMinor(source.CountryVersion, siblings.Select(s => s.CountryVersion)),
            Texts = source.Texts.Select(t => new ClaimLocalizedText { LanguageCode = t.LanguageCode, Text = t.Text })
                .ToList(),
            Qualifiers = source.Qualifiers
                .Select(t => new ClaimLocalizedText { LanguageCode = t.LanguageCode, Text = t.Text }).ToList(),
            AdaptationTypeCode = source.AdaptationTypeCode,
            AdaptationReason = source.AdaptationReason,
            AudienceProfileIds = source.AudienceProfileIds.ToList(),
            ValidFrom = source.ValidFrom,
            ValidTo = source.ValidTo,
            Status = ClaimStatuses.Draft,
            SupersedesVersionId = source.Id,
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _countryVersions.InsertAsync(entity, cancellationToken);
        await ClaimCountryVersionAudit.PublishAsync(_audit, ClaimReasonCodes.CountryVersionCreated, tenantId, entity,
            cancellationToken);
        // WP-CL-BE-5 — copy the source version's OWN active evidence (inherited core evidence is not copied: it is
        // inherited again from the newly bound claim record).
        await ClaimEvidenceCopy.CopyAsync(_evidence, _audit, _logger, tenantId, ClaimEvidenceRules.For(source),
            ClaimEvidenceRules.For(entity), ContentCompositionAuditEntities.ClaimCountryVersion, entity.Id,
            entity.Version, $"{entity.ClaimCode}|{entity.CountryCode}", cancellationToken);
        return Response<Guid>.Success(entity.Id, 201);
    }
}

public sealed class ArchiveClaimCountryVersionHandler
    : IRequestHandler<ArchiveClaimCountryVersionCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IClaimCountryVersionRepository _countryVersions;
    private readonly IContentCompositionAuditPublisher? _audit;

    public ArchiveClaimCountryVersionHandler(
        ITenantContext tenant, IActorContext actor, IClaimCountryVersionRepository countryVersions,
        IContentCompositionAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _countryVersions = countryVersions;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(ArchiveClaimCountryVersionCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var version = await _countryVersions.GetByIdAsync(tenantId, request.CountryVersionId, cancellationToken);
        if (version is null)
        {
            return Response<bool>.Fail("Country version not found.", 404);
        }

        if (version.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        if (version.Status == ClaimStatuses.InReview)
        {
            return ClaimReviewRules.InReviewLocked<bool>();
        }

        var now = DateTimeOffset.UtcNow;
        version.Status = ClaimStatuses.Archived;
        version.ArchivedAt = now;
        version.ArchivedBy = _actor.ActorName;
        version.UpdatedAt = now;
        version.UpdatedBy = _actor.ActorName;
        await _countryVersions.UpdateAsync(version, cancellationToken);
        await ClaimCountryVersionAudit.PublishAsync(_audit, ClaimReasonCodes.CountryVersionArchived, tenantId, version,
            cancellationToken);
        return Response<bool>.Success(true);
    }
}
