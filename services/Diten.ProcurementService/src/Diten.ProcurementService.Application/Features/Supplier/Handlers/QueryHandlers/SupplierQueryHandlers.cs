using Diten.Shared.Core;
using Diten.ProcurementService.Application.Features.Supplier.Queries;
using Diten.ProcurementService.Application.Features.Supplier.Store;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Handlers.QueryHandlers;

/// <summary>listSuppliers — the caller company's (non-deleted) relationships; the 1.2.0 cursor behaviour is kept as it is (§20).</summary>
public sealed class GetSupplierListHandler(ISupplierStore store) : IRequestHandler<GetSupplierListQuery, Response<SupplierListResultDto>>
{
    // Cursor sayfa boyutu (contract listSuppliers cursor). Sabit; policy netleşince config'e taşınır.
    private const int PageSize = 50;

    public async Task<Response<SupplierListResultDto>> Handle(GetSupplierListQuery request, CancellationToken cancellationToken)
    {
        var all = await store.ListAsync(request.Status, cancellationToken);

        // Cursor = son dönen SupplierId (opaque). Kararlı sıralama Name → SupplierId ikincil (önceden var olan davranış; §3.1, §20).
        var ordered = all
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.SupplierId, StringComparer.Ordinal)
            .ToList();

        IEnumerable<SupplierSnapshot> page = ordered;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            page = ordered.Where(x => string.CompareOrdinal(x.SupplierId, request.Cursor) > 0);
        }

        var pageList = page.Take(PageSize + 1).ToList();
        var hasMore = pageList.Count > PageSize;
        var items = pageList.Take(PageSize).Select(SupplierMapping.ToListItem).ToList();
        var nextCursor = hasMore ? pageList[PageSize - 1].SupplierId : null;

        return Response<SupplierListResultDto>.Success(new SupplierListResultDto(items, nextCursor, SupplierContract.Version));
    }
}

public sealed class GetSupplierByIdHandler(ISupplierStore store) : IRequestHandler<GetSupplierByIdQuery, Response<SupplierDetailDto>>
{
    public async Task<Response<SupplierDetailDto>> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
        => await store.FindAsync(request.SupplierId, cancellationToken) is { } s
            ? Response<SupplierDetailDto>.Success(SupplierMapping.ToDetail(s))
            : Response<SupplierDetailDto>.Fail("UNKNOWN_SUPPLIER", 404);
}

public sealed class GetOnboardingCaseHandler(ISupplierStore store) : IRequestHandler<GetOnboardingCaseQuery, Response<OnboardingCaseDto>>
{
    public async Task<Response<OnboardingCaseDto>> Handle(GetOnboardingCaseQuery request, CancellationToken cancellationToken)
        => await store.FindAsync(request.SupplierId, cancellationToken) is { } s
            ? Response<OnboardingCaseDto>.Success(SupplierMapping.ToOnboardingCase(s))
            : Response<OnboardingCaseDto>.Fail("UNKNOWN_SUPPLIER", 404);
}

/// <summary>
/// validateSuppliers (§3.2): one row per requested value (blank skipped, duplicates once — today's rule); codes only (a
/// uuid is not resolved here). No relationship → <c>known: false</c> and nulls — 200, never 404 (a bulk check).
/// </summary>
public sealed class ValidateSuppliersHandler(ISupplierStore store) : IRequestHandler<ValidateSuppliersQuery, Response<SupplierValidateResponseDto>>
{
    public async Task<Response<SupplierValidateResponseDto>> Handle(ValidateSuppliersQuery request, CancellationToken cancellationToken)
    {
        var requested = (request.SupplierIds ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var found = requested.Count == 0
            ? new Dictionary<string, SupplierSnapshot>()
            : await store.FindManyAsync(requested, cancellationToken);
        var rows = requested
            .Select(code => SupplierMapping.ToValidationRow(code, found.TryGetValue(code, out var s) ? s : null))
            .ToList();

        return Response<SupplierValidateResponseDto>.Success(new SupplierValidateResponseDto(rows, SupplierContract.Version));
    }
}

/// <summary>getSupplierEligibility (§3.1): code (canonical, then the caller's legacy code) or <c>supplierMasterId</c> uuid.</summary>
public sealed class GetSupplierEligibilityHandler(ISupplierStore store) : IRequestHandler<GetSupplierEligibilityQuery, Response<SupplierEligibilityDto>>
{
    public async Task<Response<SupplierEligibilityDto>> Handle(GetSupplierEligibilityQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SupplierId))
        {
            return Response<SupplierEligibilityDto>.Fail("UNKNOWN_SUPPLIER", 404);
        }

        var s = Guid.TryParse(request.SupplierId, out var masterId)
            ? await store.FindByMasterIdAsync(masterId, cancellationToken)
            : await store.FindAsync(request.SupplierId, cancellationToken);

        return s is null
            ? Response<SupplierEligibilityDto>.Fail("UNKNOWN_SUPPLIER", 404)
            : Response<SupplierEligibilityDto>.Success(SupplierMapping.ToEligibility(s));
    }
}

/// <summary>
/// listEligibleSuppliers (§3.1, F-2): exactly the caller company's eligible relationships (S-1), ordered by canonical code
/// (ordinal, ascending) only; cursor = the last returned code, filter <c>code &gt; cursor</c>; page size 50 like listSuppliers.
/// </summary>
public sealed class ListEligibleSuppliersHandler(ISupplierStore store) : IRequestHandler<ListEligibleSuppliersQuery, Response<EligibleSupplierListDto>>
{
    private const int PageSize = 50;

    public async Task<Response<EligibleSupplierListDto>> Handle(ListEligibleSuppliersQuery request, CancellationToken cancellationToken)
    {
        var cursor = string.IsNullOrWhiteSpace(request.Cursor) ? null : request.Cursor;
        var page = await store.ListEligibleAsync(cursor, PageSize + 1, cancellationToken);
        var items = page.Take(PageSize).Select(SupplierMapping.ToEligibility).ToList();
        var nextCursor = page.Count > PageSize ? items[^1].SupplierId : null;

        return Response<EligibleSupplierListDto>.Success(new EligibleSupplierListDto(items, nextCursor, SupplierContract.Version));
    }
}

/// <summary>1.4.0 getSupplierRelationship — the caller company's relationship; 404 under <c>PerLegalEntity</c>.</summary>
public sealed class GetSupplierRelationshipHandler(ISupplierStore store) : IRequestHandler<GetSupplierRelationshipQuery, Response<SupplierRelationshipDto>>
{
    public async Task<Response<SupplierRelationshipDto>> Handle(GetSupplierRelationshipQuery request, CancellationToken cancellationToken)
        => store.IsTenantShared && await store.FindAsync(request.SupplierId, cancellationToken) is { } s
            ? Response<SupplierRelationshipDto>.Success(SupplierMapping.ToRelationship(s))
            : Response<SupplierRelationshipDto>.Fail("UNKNOWN_SUPPLIER", 404);
}
