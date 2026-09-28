using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

public sealed class ListClaimsHandler : IRequestHandler<ListClaimsQuery, Response<ClaimListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IClaimRepository _claims;
    private readonly IClaimCountryVersionRepository? _countryVersions;

    public ListClaimsHandler(
        ITenantContext tenant, IClaimRepository claims, IClaimCountryVersionRepository? countryVersions = null)
    {
        _tenant = tenant;
        _claims = claims;
        _countryVersions = countryVersions;
    }

    public async Task<Response<ClaimListDto>> Handle(ListClaimsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ClaimListDto>.Fail("Tenant context is required.", 400);
        }

        IEnumerable<Claim> rows = await _claims.ListAsync(tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = ClaimStatuses.Normalize(request.Status);
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
                x.ClaimName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.ClaimCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(x => !x.IsArchived());
        }

        // WP-CL-BE-1 — list-row country summary (one tenant read, grouped in memory).
        var versionsByCode = _countryVersions is null
            ? new Dictionary<string, List<ClaimCountryVersion>>(StringComparer.Ordinal)
            : (await _countryVersions.ListAsync(tenantId, cancellationToken))
                .GroupBy(v => v.ClaimCode, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var items = rows.Select(c => ClaimMapper.ToDto(c, ClaimLines.Summary(c,
                versionsByCode.TryGetValue(c.ClaimCode, out var list) ? list : new List<ClaimCountryVersion>())))
            .ToList();
        return Response<ClaimListDto>.Success(new ClaimListDto(items, items.Count));
    }
}

public sealed class GetClaimHandler : IRequestHandler<GetClaimQuery, Response<ClaimDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IClaimRepository _claims;
    private readonly IClaimCountryVersionRepository? _countryVersions;

    public GetClaimHandler(
        ITenantContext tenant, IClaimRepository claims, IClaimCountryVersionRepository? countryVersions = null)
    {
        _tenant = tenant;
        _claims = claims;
        _countryVersions = countryVersions;
    }

    public async Task<Response<ClaimDto>> Handle(GetClaimQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ClaimDto>.Fail("Tenant context is required.", 400);
        }

        var entity = await _claims.GetByIdAsync(tenantId, request.ClaimId, cancellationToken);
        if (entity is null)
        {
            return Response<ClaimDto>.Fail("Claim not found.", 404);
        }

        var versions = _countryVersions is null
            ? Array.Empty<ClaimCountryVersion>()
            : await _countryVersions.ListByClaimCodeAsync(tenantId, entity.ClaimCode, cancellationToken);
        return Response<ClaimDto>.Success(ClaimMapper.ToDto(entity, ClaimLines.Summary(entity, versions)));
    }
}

// ---------------------------------------------------------------- WP-CL-BE-1 (claims v2)

/// <summary>Every country version of the claim's ClaimCode (all core versions), optionally one country.</summary>
public sealed class ListClaimCountryVersionsHandler
    : IRequestHandler<ListClaimCountryVersionsQuery, Response<IReadOnlyList<ClaimCountryVersionDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly IClaimRepository _claims;
    private readonly IClaimCountryVersionRepository _countryVersions;
    private readonly IClaimCoverageSettings? _settings;

    public ListClaimCountryVersionsHandler(
        ITenantContext tenant, IClaimRepository claims, IClaimCountryVersionRepository countryVersions,
        IClaimCoverageSettings? settings = null)
    {
        _tenant = tenant;
        _claims = claims;
        _countryVersions = countryVersions;
        _settings = settings;
    }

    public async Task<Response<IReadOnlyList<ClaimCountryVersionDto>>> Handle(
        ListClaimCountryVersionsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<IReadOnlyList<ClaimCountryVersionDto>>.Fail("Tenant context is required.", 400);
        }

        var claim = await _claims.GetByIdAsync(tenantId, request.ClaimId, cancellationToken);
        if (claim is null)
        {
            return Response<IReadOnlyList<ClaimCountryVersionDto>>.Fail("Claim not found.", 404);
        }

        IEnumerable<ClaimCountryVersion> rows =
            await _countryVersions.ListByClaimCodeAsync(tenantId, claim.ClaimCode, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.CountryCode))
        {
            var country = ClaimV2Checks.NormalizeCountry(request.CountryCode);
            rows = rows.Where(v => string.Equals(v.CountryCode, country, StringComparison.OrdinalIgnoreCase));
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(v => !v.IsArchived());
        }

        var now = DateTimeOffset.UtcNow;
        var window = _settings?.ExpiringWindowDays ?? ClaimCoverageDefaults.ExpiringWindowDays;
        IReadOnlyList<ClaimCountryVersionDto> items = rows
            .Select(v => ClaimMapper.ToDto(v, v.IsLive() && ClaimLines.IsExpiring(v, now, window)))
            .ToList();
        return Response<IReadOnlyList<ClaimCountryVersionDto>>.Success(items);
    }
}

public sealed class GetClaimCountryVersionHandler
    : IRequestHandler<GetClaimCountryVersionQuery, Response<ClaimCountryVersionDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IClaimCountryVersionRepository _countryVersions;
    private readonly IClaimCoverageSettings? _settings;

    public GetClaimCountryVersionHandler(
        ITenantContext tenant, IClaimCountryVersionRepository countryVersions, IClaimCoverageSettings? settings = null)
    {
        _tenant = tenant;
        _countryVersions = countryVersions;
        _settings = settings;
    }

    public async Task<Response<ClaimCountryVersionDto>> Handle(
        GetClaimCountryVersionQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ClaimCountryVersionDto>.Fail("Tenant context is required.", 400);
        }

        var version = await _countryVersions.GetByIdAsync(tenantId, request.CountryVersionId, cancellationToken);
        if (version is null)
        {
            return Response<ClaimCountryVersionDto>.Fail("Country version not found.", 404);
        }

        var window = _settings?.ExpiringWindowDays ?? ClaimCoverageDefaults.ExpiringWindowDays;
        return Response<ClaimCountryVersionDto>.Success(ClaimMapper.ToDto(version,
            version.IsLive() && ClaimLines.IsExpiring(version, DateTimeOffset.UtcNow, window)));
    }
}

/// <summary>
/// WP-CL-BE-1 — the claim × country coverage matrix. Columns are the ACTIVE values of the <c>COUNTRY_CODES</c> set in
/// BRD order (never a compiled list — an unpublished set is <c>reference_set_missing</c>, fail-closed). Rows are the
/// current record of each claim code (fully archived codes are left out).
/// </summary>
public sealed class GetClaimCoverageHandler : IRequestHandler<GetClaimCoverageQuery, Response<ClaimCoverageDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IClaimRepository _claims;
    private readonly IClaimCountryVersionRepository _countryVersions;
    private readonly IReferenceDataCatalogReader? _catalog;
    private readonly IClaimCoverageSettings? _settings;

    public GetClaimCoverageHandler(
        ITenantContext tenant, IClaimRepository claims, IClaimCountryVersionRepository countryVersions,
        IReferenceDataCatalogReader? catalog = null, IClaimCoverageSettings? settings = null)
    {
        _tenant = tenant;
        _claims = claims;
        _countryVersions = countryVersions;
        _catalog = catalog;
        _settings = settings;
    }

    public async Task<Response<ClaimCoverageDto>> Handle(GetClaimCoverageQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ClaimCoverageDto>.Fail("Tenant context is required.", 400);
        }

        if (!string.IsNullOrWhiteSpace(request.Kind) && !ClaimKinds.IsValid(request.Kind))
        {
            return new ClaimFailure(ClaimErrorCodes.InvalidKind,
                $"Kind must be one of: {string.Join(", ", ClaimKinds.All)}.").To<ClaimCoverageDto>();
        }

        var set = _catalog is null
            ? null
            : await _catalog.GetPublishedValuesAsync(ClaimReferenceSets.CountryCodes, cancellationToken);
        if (set is null || !set.IsPublished)
        {
            return new ClaimFailure(ClaimErrorCodes.ReferenceSetMissing,
                $"Reference set '{ClaimReferenceSets.CountryCodes}' is not published; the country axis is unavailable.")
                .To<ClaimCoverageDto>();
        }

        var countries = set.Values.Where(v => v.IsActive && !v.IsDeprecated)
            .Select(v => new ClaimCoverageCountryDto(ClaimV2Checks.NormalizeCountry(v.ValueCode), v.DisplayName))
            .GroupBy(c => c.CountryCode).Select(g => g.First())
            .ToList();

        var versionsByCode = (await _countryVersions.ListAsync(tenantId, cancellationToken))
            .GroupBy(v => v.ClaimCode, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var now = DateTimeOffset.UtcNow;
        var window = _settings?.ExpiringWindowDays ?? ClaimCoverageDefaults.ExpiringWindowDays;
        var kind = string.IsNullOrWhiteSpace(request.Kind) ? null : request.Kind.Trim().ToLowerInvariant();
        var status = string.IsNullOrWhiteSpace(request.Status) ? null : ClaimStatuses.Normalize(request.Status);
        var term = request.Search?.Trim();

        var rows = (await _claims.ListAsync(tenantId, cancellationToken))
            .GroupBy(c => c.ClaimCode, StringComparer.Ordinal)
            .Select(g => ClaimLines.Current(g))
            .OfType<Claim>()
            .Where(c => request.ProductId is null || c.ProductId == request.ProductId)
            .Where(c => kind is null || KindOf(c) == kind)
            .Where(c => status is null || c.Status == status)
            .Where(c => string.IsNullOrEmpty(term)
                || c.ClaimCode.Contains(term, StringComparison.OrdinalIgnoreCase)
                || c.ClaimName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.ClaimCode, StringComparer.Ordinal)
            .Select(c =>
            {
                var versions = versionsByCode.TryGetValue(c.ClaimCode, out var list)
                    ? list
                    : new List<ClaimCountryVersion>();
                return new ClaimCoverageRowDto(
                    c.Id, c.ClaimCode, c.ClaimName, KindOf(c), c.LocalCountryCode, c.ProductId, c.ProductDisplay,
                    c.AudienceProfileIds.Count, c.ClaimVersion, c.Status,
                    countries.Select(country => ClaimLines.Cell(c, country.CountryCode, versions, now, window))
                        .ToList());
            })
            .ToList();

        return Response<ClaimCoverageDto>.Success(new ClaimCoverageDto(countries, rows, window));
    }

    private static string KindOf(Claim c) => string.IsNullOrWhiteSpace(c.Kind) ? ClaimKinds.Core : c.Kind;
}
