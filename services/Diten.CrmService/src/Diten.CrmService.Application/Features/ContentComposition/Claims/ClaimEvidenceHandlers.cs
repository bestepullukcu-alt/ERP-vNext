using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

// WP-CL-BE-5 — the claim evidence surface: list (with inheritance), link, remove and the document picker. The ObjectRef
// is always built here from the addressed record; a request body can never name it.

public sealed record ClaimEvidenceItemDto(
    Guid LinkId,
    string Origin,
    string ObjectType,
    string ObjectId,
    string? ObjectVersion,
    string DocumentKind,
    Guid DocumentId,
    Guid? DocumentVersionId,
    string? DocumentVersionLabel,
    string DocumentTitle,
    string EvidenceTypeCode,
    ClaimEvidenceLocator Locator,
    IReadOnlyList<ClaimEvidenceSpan> SupportedSpans,
    string Status,
    string? LinkedBy,
    DateTimeOffset LinkedAt,
    string? RemovedBy,
    DateTimeOffset? RemovedAt,
    string? RemovalReason,
    Guid? CurrentVersionId,
    string? CurrentVersionLabel,
    bool IsSuperseded,
    string? DocumentState,
    DateTimeOffset? ReviewDueAt,
    bool NeedsReview,
    bool IsExpiring);

public sealed record ClaimEvidenceListDto(
    IReadOnlyList<ClaimEvidenceItemDto> Items,
    int EffectiveCount,
    bool AnyNeedsReview,
    bool AnyExpiring,
    bool Locked);

public sealed record GetClaimEvidenceQuery(Guid ClaimId, bool IncludeRemoved = false)
    : IRequest<Response<ClaimEvidenceListDto>>;

public sealed record GetClaimCountryVersionEvidenceQuery(Guid CountryVersionId, bool IncludeRemoved = false)
    : IRequest<Response<ClaimEvidenceListDto>>;

public sealed record LinkClaimEvidenceCommand(Guid ClaimId, ClaimEvidenceLinkInput Input)
    : IRequest<Response<ClaimEvidenceItemDto>>;

public sealed record LinkClaimCountryVersionEvidenceCommand(Guid CountryVersionId, ClaimEvidenceLinkInput Input)
    : IRequest<Response<ClaimEvidenceItemDto>>;

public sealed record RemoveClaimEvidenceCommand(Guid LinkId, string? Reason) : IRequest<Response<ClaimEvidenceItemDto>>;

public sealed record GetClaimEvidenceDocumentOptionsQuery(string? Search, string? Kind)
    : IRequest<Response<IReadOnlyList<ClaimEvidenceDocumentOptionDto>>>;

internal static class ClaimEvidenceMapping
{
    public static ClaimEvidenceItemDto ToDto(ClaimEvidenceLink l, string origin, DateTimeOffset now, int windowDays) => new(
        l.LinkId, origin, l.ObjectRef.ObjectType, l.ObjectRef.ObjectId, l.ObjectRef.ObjectVersion, l.DocumentKind,
        l.DocumentId, l.DocumentVersionId, l.DocumentVersionLabel, l.DocumentTitle, l.EvidenceTypeCode, l.Locator,
        l.SupportedSpans, l.Status, l.LinkedBy, l.LinkedAt, l.RemovedBy, l.RemovedAt, l.RemovalReason, l.CurrentVersionId,
        l.CurrentVersionLabel, l.IsSuperseded, l.DocumentState, l.ReviewDueAt, ClaimEvidenceRules.NeedsReview(l),
        ClaimEvidenceRules.IsExpiring(l, now, windowDays));

    public static ClaimEvidenceListDto List(IEnumerable<ClaimEvidenceItemDto> items, bool locked)
    {
        var rows = items.OrderBy(i => i.Origin == ClaimEvidenceRules.OriginCore ? 0 : 1)
            .ThenByDescending(i => i.LinkedAt).ToList();
        var active = rows.Where(i => string.Equals(i.Status, "active", StringComparison.OrdinalIgnoreCase)).ToList();
        return new ClaimEvidenceListDto(rows, active.Count, active.Any(i => i.NeedsReview), active.Any(i => i.IsExpiring),
            locked);
    }

    public static string OriginOf(Claim c) => c.IsLocal() ? ClaimEvidenceRules.OriginLocal : ClaimEvidenceRules.OriginCore;

    public static int Window(IClaimCoverageSettings? settings) =>
        settings?.ExpiringWindowDays ?? ClaimCoverageDefaults.ExpiringWindowDays;

    public static bool Owns(ClaimEvidenceLink l, string objectType, Guid id) =>
        l.ObjectRef.Module == ClaimEvidenceRules.Module && l.ObjectRef.ObjectType == objectType
        && string.Equals(l.ObjectRef.ObjectId, id.ToString("D"), StringComparison.OrdinalIgnoreCase);
}

public sealed class GetClaimEvidenceHandler : IRequestHandler<GetClaimEvidenceQuery, Response<ClaimEvidenceListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IClaimRepository _claims;
    private readonly IClaimEvidenceClient _evidence;
    private readonly IClaimCoverageSettings? _settings;
    private readonly TimeProvider _clock;

    public GetClaimEvidenceHandler(ITenantContext tenant, IClaimRepository claims, IClaimEvidenceClient evidence,
        IClaimCoverageSettings? settings = null, TimeProvider? clock = null)
    {
        _tenant = tenant;
        _claims = claims;
        _evidence = evidence;
        _settings = settings;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<ClaimEvidenceListDto>> Handle(GetClaimEvidenceQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ClaimEvidenceListDto>.Fail("Tenant context is required.", 400);
        }

        var claim = await _claims.GetByIdAsync(tenantId, request.ClaimId, ct);
        if (claim is null)
        {
            return Response<ClaimEvidenceListDto>.Fail("Claim not found.", 404);
        }

        var links = await _evidence.QueryAsync(
            [ClaimEvidenceRules.ReadKey(ClaimEvidenceRules.ClaimObjectType, claim.Id)], request.IncludeRemoved, ct);
        if (links is null)
        {
            return ClaimEvidenceRules.Unavailable<ClaimEvidenceListDto>();
        }

        var now = _clock.GetUtcNow();
        var window = ClaimEvidenceMapping.Window(_settings);
        var origin = ClaimEvidenceMapping.OriginOf(claim);
        var items = links.Where(l => ClaimEvidenceMapping.Owns(l, ClaimEvidenceRules.ClaimObjectType, claim.Id))
            .Select(l => ClaimEvidenceMapping.ToDto(l, origin, now, window));
        return Response<ClaimEvidenceListDto>.Success(
            ClaimEvidenceMapping.List(items, ClaimEvidenceRules.IsLocked(claim.Status, claim.IsArchived())));
    }
}

public sealed class GetClaimCountryVersionEvidenceHandler
    : IRequestHandler<GetClaimCountryVersionEvidenceQuery, Response<ClaimEvidenceListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IClaimCountryVersionRepository _versions;
    private readonly IClaimEvidenceClient _evidence;
    private readonly IClaimCoverageSettings? _settings;
    private readonly TimeProvider _clock;

    public GetClaimCountryVersionEvidenceHandler(ITenantContext tenant, IClaimCountryVersionRepository versions,
        IClaimEvidenceClient evidence, IClaimCoverageSettings? settings = null, TimeProvider? clock = null)
    {
        _tenant = tenant;
        _versions = versions;
        _evidence = evidence;
        _settings = settings;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<ClaimEvidenceListDto>> Handle(GetClaimCountryVersionEvidenceQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ClaimEvidenceListDto>.Fail("Tenant context is required.", 400);
        }

        var version = await _versions.GetByIdAsync(tenantId, request.CountryVersionId, ct);
        if (version is null)
        {
            return Response<ClaimEvidenceListDto>.Fail("Country version not found.", 404);
        }

        var links = await _evidence.QueryAsync(
        [
            ClaimEvidenceRules.ReadKey(ClaimEvidenceRules.CountryVersionObjectType, version.Id),
            ClaimEvidenceRules.ReadKey(ClaimEvidenceRules.ClaimObjectType, version.ClaimId)
        ], request.IncludeRemoved, ct);
        if (links is null)
        {
            return ClaimEvidenceRules.Unavailable<ClaimEvidenceListDto>();
        }

        var now = _clock.GetUtcNow();
        var window = ClaimEvidenceMapping.Window(_settings);
        // Inherited = the bound claim record's ACTIVE links (read-only here); own = this version's links.
        var inherited = links
            .Where(l => l.IsActive && ClaimEvidenceMapping.Owns(l, ClaimEvidenceRules.ClaimObjectType, version.ClaimId))
            .Select(l => ClaimEvidenceMapping.ToDto(l, ClaimEvidenceRules.OriginCore, now, window));
        var own = links
            .Where(l => ClaimEvidenceMapping.Owns(l, ClaimEvidenceRules.CountryVersionObjectType, version.Id))
            .Select(l => ClaimEvidenceMapping.ToDto(l, ClaimEvidenceRules.OriginLocal, now, window));
        return Response<ClaimEvidenceListDto>.Success(ClaimEvidenceMapping.List(inherited.Concat(own),
            ClaimEvidenceRules.IsLocked(version.Status, version.IsArchived())));
    }
}

public sealed class LinkClaimEvidenceHandler : IRequestHandler<LinkClaimEvidenceCommand, Response<ClaimEvidenceItemDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IClaimRepository _claims;
    private readonly IClaimEvidenceClient _evidence;
    private readonly IContentCompositionAuditPublisher? _audit;
    private readonly IClaimCoverageSettings? _settings;

    public LinkClaimEvidenceHandler(ITenantContext tenant, IClaimRepository claims, IClaimEvidenceClient evidence,
        IContentCompositionAuditPublisher? audit = null, IClaimCoverageSettings? settings = null)
    {
        _tenant = tenant;
        _claims = claims;
        _evidence = evidence;
        _audit = audit;
        _settings = settings;
    }

    public async Task<Response<ClaimEvidenceItemDto>> Handle(LinkClaimEvidenceCommand request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ClaimEvidenceItemDto>.Fail("Tenant context is required.", 400);
        }

        var claim = await _claims.GetByIdAsync(tenantId, request.ClaimId, ct);
        if (claim is null)
        {
            return Response<ClaimEvidenceItemDto>.Fail("Claim not found.", 404);
        }

        if (ClaimEvidenceRules.IsLocked(claim.Status, claim.IsArchived()))
        {
            return ClaimEvidenceRules.Locked<ClaimEvidenceItemDto>();
        }

        var result = await _evidence.LinkAsync(ClaimEvidenceRules.For(claim), request.Input, ct);
        if (result.Outcome != ClaimEvidenceCallOutcome.Ok || result.Data is not { } link)
        {
            return ClaimEvidenceRules.FromCall<ClaimEvidenceItemDto, ClaimEvidenceLink>(result);
        }

        if (_audit is not null)
        {
            await _audit.PublishAsync(ClaimReasonCodes.EvidenceLinked, tenantId, ContentCompositionAuditEntities.Claim,
                claim.Id, claim.Version, $"{claim.ClaimCode}|link={link.LinkId:D}|document={link.DocumentId:D}", ct);
        }

        return Response<ClaimEvidenceItemDto>.Success(ClaimEvidenceMapping.ToDto(link, ClaimEvidenceMapping.OriginOf(claim),
            DateTimeOffset.UtcNow, ClaimEvidenceMapping.Window(_settings)), 201);
    }
}

public sealed class LinkClaimCountryVersionEvidenceHandler
    : IRequestHandler<LinkClaimCountryVersionEvidenceCommand, Response<ClaimEvidenceItemDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IClaimCountryVersionRepository _versions;
    private readonly IClaimEvidenceClient _evidence;
    private readonly IContentCompositionAuditPublisher? _audit;
    private readonly IClaimCoverageSettings? _settings;

    public LinkClaimCountryVersionEvidenceHandler(ITenantContext tenant, IClaimCountryVersionRepository versions,
        IClaimEvidenceClient evidence, IContentCompositionAuditPublisher? audit = null,
        IClaimCoverageSettings? settings = null)
    {
        _tenant = tenant;
        _versions = versions;
        _evidence = evidence;
        _audit = audit;
        _settings = settings;
    }

    public async Task<Response<ClaimEvidenceItemDto>> Handle(LinkClaimCountryVersionEvidenceCommand request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ClaimEvidenceItemDto>.Fail("Tenant context is required.", 400);
        }

        var version = await _versions.GetByIdAsync(tenantId, request.CountryVersionId, ct);
        if (version is null)
        {
            return Response<ClaimEvidenceItemDto>.Fail("Country version not found.", 404);
        }

        if (ClaimEvidenceRules.IsLocked(version.Status, version.IsArchived()))
        {
            return ClaimEvidenceRules.Locked<ClaimEvidenceItemDto>();
        }

        var result = await _evidence.LinkAsync(ClaimEvidenceRules.For(version), request.Input, ct);
        if (result.Outcome != ClaimEvidenceCallOutcome.Ok || result.Data is not { } link)
        {
            return ClaimEvidenceRules.FromCall<ClaimEvidenceItemDto, ClaimEvidenceLink>(result);
        }

        if (_audit is not null)
        {
            await _audit.PublishAsync(ClaimReasonCodes.EvidenceLinked, tenantId,
                ContentCompositionAuditEntities.ClaimCountryVersion, version.Id, version.Version,
                $"{version.ClaimCode}|{version.CountryCode}|link={link.LinkId:D}|document={link.DocumentId:D}", ct);
        }

        return Response<ClaimEvidenceItemDto>.Success(ClaimEvidenceMapping.ToDto(link, ClaimEvidenceRules.OriginLocal,
            DateTimeOffset.UtcNow, ClaimEvidenceMapping.Window(_settings)), 201);
    }
}

/// <summary>Removes a link of a CRM claim record. The link must belong to a claim or country version of THIS tenant
/// (anything else — another module, another object type, an unknown record — is 404) and that record must be a draft.</summary>
public sealed class RemoveClaimEvidenceHandler : IRequestHandler<RemoveClaimEvidenceCommand, Response<ClaimEvidenceItemDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IClaimRepository _claims;
    private readonly IClaimCountryVersionRepository _versions;
    private readonly IClaimEvidenceClient _evidence;
    private readonly IContentCompositionAuditPublisher? _audit;
    private readonly IClaimCoverageSettings? _settings;

    public RemoveClaimEvidenceHandler(ITenantContext tenant, IClaimRepository claims,
        IClaimCountryVersionRepository versions, IClaimEvidenceClient evidence,
        IContentCompositionAuditPublisher? audit = null, IClaimCoverageSettings? settings = null)
    {
        _tenant = tenant;
        _claims = claims;
        _versions = versions;
        _evidence = evidence;
        _audit = audit;
        _settings = settings;
    }

    public async Task<Response<ClaimEvidenceItemDto>> Handle(RemoveClaimEvidenceCommand request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ClaimEvidenceItemDto>.Fail("Tenant context is required.", 400);
        }

        var found = await _evidence.GetAsync(request.LinkId, ct);
        if (found.Outcome == ClaimEvidenceCallOutcome.Unavailable)
        {
            return ClaimEvidenceRules.Unavailable<ClaimEvidenceItemDto>();
        }

        if (found.Outcome != ClaimEvidenceCallOutcome.Ok || found.Data is not { } link
            || link.ObjectRef.Module != ClaimEvidenceRules.Module
            || !Guid.TryParse(link.ObjectRef.ObjectId, out var objectId))
        {
            return NotFound();
        }

        string entityType;
        Guid entityId;
        int entityVersion;
        string detail;
        string origin;
        switch (link.ObjectRef.ObjectType)
        {
            case ClaimEvidenceRules.ClaimObjectType:
            {
                var claim = await _claims.GetByIdAsync(tenantId, objectId, ct);
                if (claim is null)
                {
                    return NotFound();
                }

                if (ClaimEvidenceRules.IsLocked(claim.Status, claim.IsArchived()))
                {
                    return ClaimEvidenceRules.Locked<ClaimEvidenceItemDto>();
                }

                (entityType, entityId, entityVersion, detail, origin) = (ContentCompositionAuditEntities.Claim, claim.Id,
                    claim.Version, claim.ClaimCode, ClaimEvidenceMapping.OriginOf(claim));
                break;
            }
            case ClaimEvidenceRules.CountryVersionObjectType:
            {
                var version = await _versions.GetByIdAsync(tenantId, objectId, ct);
                if (version is null)
                {
                    return NotFound();
                }

                if (ClaimEvidenceRules.IsLocked(version.Status, version.IsArchived()))
                {
                    return ClaimEvidenceRules.Locked<ClaimEvidenceItemDto>();
                }

                (entityType, entityId, entityVersion, detail, origin) = (ContentCompositionAuditEntities.ClaimCountryVersion,
                    version.Id, version.Version, $"{version.ClaimCode}|{version.CountryCode}", ClaimEvidenceRules.OriginLocal);
                break;
            }
            default:
                return NotFound();
        }

        var result = await _evidence.RemoveAsync(request.LinkId, request.Reason, ct);
        if (result.Outcome != ClaimEvidenceCallOutcome.Ok || result.Data is not { } removed)
        {
            return ClaimEvidenceRules.FromCall<ClaimEvidenceItemDto, ClaimEvidenceLink>(result);
        }

        if (_audit is not null)
        {
            await _audit.PublishAsync(ClaimReasonCodes.EvidenceRemoved, tenantId, entityType, entityId, entityVersion,
                $"{detail}|link={removed.LinkId:D}|document={removed.DocumentId:D}", ct);
        }

        return Response<ClaimEvidenceItemDto>.Success(ClaimEvidenceMapping.ToDto(removed, origin, DateTimeOffset.UtcNow,
            ClaimEvidenceMapping.Window(_settings)));
    }

    private static Response<ClaimEvidenceItemDto> NotFound() =>
        Response<ClaimEvidenceItemDto>.Fail(new[] { "link_not_found", "Evidence link not found." }, 404);
}

public sealed class GetClaimEvidenceDocumentOptionsHandler
    : IRequestHandler<GetClaimEvidenceDocumentOptionsQuery, Response<IReadOnlyList<ClaimEvidenceDocumentOptionDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly IClaimEvidenceClient _evidence;

    public GetClaimEvidenceDocumentOptionsHandler(ITenantContext tenant, IClaimEvidenceClient evidence)
    {
        _tenant = tenant;
        _evidence = evidence;
    }

    public async Task<Response<IReadOnlyList<ClaimEvidenceDocumentOptionDto>>> Handle(
        GetClaimEvidenceDocumentOptionsQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is null)
        {
            return Response<IReadOnlyList<ClaimEvidenceDocumentOptionDto>>.Fail("Tenant context is required.", 400);
        }

        var result = await _evidence.GetDocumentOptionsAsync(request.Search, request.Kind, ct);
        return result.Outcome == ClaimEvidenceCallOutcome.Ok && result.Data is { } options
            ? Response<IReadOnlyList<ClaimEvidenceDocumentOptionDto>>.Success(options)
            : ClaimEvidenceRules.FromCall<IReadOnlyList<ClaimEvidenceDocumentOptionDto>,
                IReadOnlyList<ClaimEvidenceDocumentOptionDto>>(result);
    }
}
