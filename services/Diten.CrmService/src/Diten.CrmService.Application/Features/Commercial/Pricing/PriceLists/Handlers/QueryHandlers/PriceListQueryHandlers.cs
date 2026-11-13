using ClosedXML.Excel;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories.Commercial;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists.Handlers.QueryHandlers;

/// <summary>WP-VW-W6 W6-BE-b — the price list reads. Tenant scoped (another tenant's id → 404); the phase of a published list
/// is derived at read time; a list in review older than the reconcile window is checked against MOD-0023 first.</summary>
public sealed class PriceListQueryHandlers :
    IRequestHandler<GetPriceListListQuery, Response<PriceListPageDto>>,
    IRequestHandler<GetPriceListByIdQuery, Response<PriceListDto>>,
    IRequestHandler<GetPriceListItemsQuery, Response<PriceListItemPageDto>>,
    IRequestHandler<GetPriceListExportQuery, Response<PriceListExportDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IPriceListRepository _lists;
    private readonly IPriceListItemRepository _items;
    private readonly PricingApprovalReconciler? _reconciler;
    private readonly TimeProvider _clock;

    public PriceListQueryHandlers(ITenantContext tenant, IPriceListRepository lists, IPriceListItemRepository items,
        PricingApprovalReconciler? reconciler = null, TimeProvider? clock = null)
    {
        _tenant = tenant;
        _lists = lists;
        _items = items;
        _reconciler = reconciler;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<PriceListPageDto>> Handle(GetPriceListListQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PriceListPageDto>("Tenant context is required.");
        }

        var (page, size) = Paging(request.Page, request.PageSize);
        var status = PricingCommon.Clean(request.Status)?.ToLowerInvariant();
        var isPhase = PricingCommon.In(status, PriceListPhases.All);
        if (status is not null && !isPhase && !PricingCommon.In(status, PriceListStatuses.All))
        {
            return PricingCommon.Invalid<PriceListPageDto>("status is a list status or a phase (planned / in-force / past).");
        }

        var filter = new PriceListFilter(request.LegalEntityId, PricingCommon.Clean(request.CountryCode)?.ToUpperInvariant(),
            request.Currency is null ? null : PricingCommon.NormalizeCurrency(request.Currency), PricingCommon.Clean(request.Kind)?.ToLowerInvariant(),
            status is null ? null : new[] { isPhase ? PriceListStatuses.Published : status }, PricingCommon.Clean(request.Search));
        var today = PricingCommon.Today(_clock);

        if (isPhase)
        {
            // a phase is derived: read the published lists of the filter, keep the phase, page in memory (lists are few)
            var (all, _) = await _lists.ListAsync(tenantId, filter, 0, int.MaxValue, ct);
            var published = await PublishedOfAsync(tenantId, all, ct);
            var rows = all.Where(l => l.TenantId == tenantId)
                .Select(l => PriceListMapping.ToDto(l, SameKey(published, l), today))
                .Where(d => d.Phase == status)
                .ToList();
            return Response<PriceListPageDto>.Success(new PriceListPageDto(rows.Skip((page - 1) * size).Take(size).ToList(), rows.Count, page, size));
        }

        var (items, total) = await _lists.ListAsync(tenantId, filter, (page - 1) * size, size, ct);
        items = await ReconcileAsync(tenantId, items, ct);
        var keys = await PublishedOfAsync(tenantId, items, ct);
        return Response<PriceListPageDto>.Success(new PriceListPageDto(
            items.Where(l => l.TenantId == tenantId).Select(l => PriceListMapping.ToDto(l, SameKey(keys, l), today)).ToList(), total, page, size));
    }

    public async Task<Response<PriceListDto>> Handle(GetPriceListByIdQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PriceListDto>("Tenant context is required.");
        }

        var list = await GetAsync(tenantId, request.Id, ct);
        if (list is null)
        {
            return PricingCommon.NotFound<PriceListDto>();
        }

        list = (await ReconcileAsync(tenantId, new[] { list }, ct)).Single();
        var sameKey = await _lists.ListPublishedAsync(tenantId, list.LegalEntityId, list.CountryCode, ct);
        return Response<PriceListDto>.Success(PriceListMapping.ToDto(list, sameKey, PricingCommon.Today(_clock)));
    }

    public async Task<Response<PriceListItemPageDto>> Handle(GetPriceListItemsQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PriceListItemPageDto>("Tenant context is required.");
        }

        var list = await GetAsync(tenantId, request.Id, ct);
        if (list is null)
        {
            return PricingCommon.NotFound<PriceListItemPageDto>();
        }

        var diff = PricingCommon.Clean(request.Diff)?.ToLowerInvariant();
        if (diff is not null && diff != "previous")
        {
            return PricingCommon.Invalid<PriceListItemPageDto>("diff is 'previous' or absent.");
        }

        var (page, size) = Paging(request.Page, request.PageSize);
        var (items, total) = await _items.ListAsync(tenantId, list.Id, PricingCommon.Clean(request.Search), (page - 1) * size, size, ct);
        items = items.Where(i => i.TenantId == tenantId && !i.IsDeleted).ToList();
        if (diff is null || list.PreviousPriceListId is not { } previousId || await GetAsync(tenantId, previousId, ct) is not { } previous)
        {
            return Response<PriceListItemPageDto>.Success(new PriceListItemPageDto(items.Select(i => PriceListMapping.ToDto(i)).ToList(), total,
                page, size, diff is null ? null : Array.Empty<PriceListItemDto>()));
        }

        var previousItems = (await _items.ListAllAsync(tenantId, previous.Id, ct)).Where(i => !i.IsDeleted && i.TenantId == tenantId)
            .GroupBy(i => i.SkuId).ToDictionary(g => g.Key, g => g.First());
        var current = (await _items.ListAllAsync(tenantId, list.Id, ct)).Where(i => !i.IsDeleted).Select(i => i.SkuId).ToHashSet();
        var removed = previousItems.Values.Where(p => !current.Contains(p.SkuId))
            .OrderBy(p => p.SkuCode, StringComparer.Ordinal)
            .Select(p => PriceListMapping.ToDto(p, new PriceListItemDiffDto(PriceListDiffKinds.Removed, p.DepotPrice, p.PharmacistPurchasePrice,
                p.PublicPrice, p.VatRate)))
            .ToList();
        return Response<PriceListItemPageDto>.Success(new PriceListItemPageDto(
            items.Select(i => PriceListMapping.ToDto(i, PriceListMapping.Diff(i, previousItems.GetValueOrDefault(i.SkuId)))).ToList(),
            total, page, size, removed));
    }

    public async Task<Response<PriceListExportDto>> Handle(GetPriceListExportQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PriceListExportDto>("Tenant context is required.");
        }

        using var workbook = new XLWorkbook();
        string fileName;
        if (request.PriceListId is { } listId)
        {
            var list = await GetAsync(tenantId, listId, ct);
            if (list is null)
            {
                return PricingCommon.NotFound<PriceListExportDto>();
            }

            var sheet = workbook.AddWorksheet("items");
            var header = new[] { "skuCode", "skuName", "gtin", "depotPrice", "pharmacistPurchasePrice", "publicPrice", "vatRate", "currency" };
            for (var c = 0; c < header.Length; c++)
            {
                sheet.Cell(1, c + 1).Value = header[c];
            }

            var row = 2;
            foreach (var item in (await _items.ListAllAsync(tenantId, list.Id, ct)).Where(i => !i.IsDeleted && i.TenantId == tenantId)
                         .OrderBy(i => i.SkuCode, StringComparer.Ordinal))
            {
                sheet.Cell(row, 1).Value = item.SkuCode;
                sheet.Cell(row, 2).Value = item.SkuName;
                sheet.Cell(row, 3).Value = item.Gtin;
                sheet.Cell(row, 4).Value = item.DepotPrice;
                sheet.Cell(row, 5).Value = item.PharmacistPurchasePrice;
                sheet.Cell(row, 6).Value = item.PublicPrice;
                sheet.Cell(row, 7).Value = item.VatRate;
                sheet.Cell(row, 8).Value = list.Currency;
                row++;
            }

            fileName = $"price-list-{list.LegalEntityCode ?? list.LegalEntityId.ToString("N")[..8]}-{list.CountryCode}-v{list.ListVersion}.xlsx";
        }
        else
        {
            var status = PricingCommon.Clean(request.Status)?.ToLowerInvariant();
            var (lists, _) = await _lists.ListAsync(tenantId, new PriceListFilter(request.LegalEntityId,
                PricingCommon.Clean(request.CountryCode)?.ToUpperInvariant(), request.Currency is null ? null : PricingCommon.NormalizeCurrency(request.Currency),
                PricingCommon.Clean(request.Kind)?.ToLowerInvariant(),
                status is null || PricingCommon.In(status, PriceListPhases.All) ? null : new[] { status }), 0, int.MaxValue, ct);
            var published = await PublishedOfAsync(tenantId, lists, ct);
            var today = PricingCommon.Today(_clock);
            var sheet = workbook.AddWorksheet("price-lists");
            var header = new[] { "name", "legalEntity", "country", "currency", "kind", "validFrom", "validTo", "status", "items", "version" };
            for (var c = 0; c < header.Length; c++)
            {
                sheet.Cell(1, c + 1).Value = header[c];
            }

            var row = 2;
            foreach (var dto in lists.Where(l => l.TenantId == tenantId).Select(l => PriceListMapping.ToDto(l, SameKey(published, l), today))
                         .Where(d => status is null || !PricingCommon.In(status, PriceListPhases.All) || d.Phase == status))
            {
                sheet.Cell(row, 1).Value = dto.Name;
                sheet.Cell(row, 2).Value = dto.LegalEntityCode ?? dto.LegalEntityName;
                sheet.Cell(row, 3).Value = dto.CountryCode;
                sheet.Cell(row, 4).Value = dto.Currency;
                sheet.Cell(row, 5).Value = dto.Kind;
                sheet.Cell(row, 6).Value = dto.ValidFrom.ToString("yyyy-MM-dd");
                sheet.Cell(row, 7).Value = dto.EffectiveValidTo?.ToString("yyyy-MM-dd");
                sheet.Cell(row, 8).Value = dto.Badge;
                sheet.Cell(row, 9).Value = dto.ItemCount;
                sheet.Cell(row, 10).Value = dto.ListVersion;
                row++;
            }

            fileName = "price-lists.xlsx";
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Response<PriceListExportDto>.Success(new PriceListExportDto(fileName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", stream.ToArray()));
    }

    private async Task<PriceList?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
        => await _lists.GetByIdAsync(tenantId, id, ct) is { } list && list.TenantId == tenantId && !list.IsDeleted ? list : null;

    private async Task<IReadOnlyList<PriceList>> PublishedOfAsync(Guid tenantId, IEnumerable<PriceList> lists, CancellationToken ct)
    {
        var keys = lists.Select(l => (l.LegalEntityId, l.CountryCode)).Distinct().ToList();
        return keys.Count == 0 ? Array.Empty<PriceList>() : await _lists.ListPublishedForAsync(tenantId, keys, ct);
    }

    private static IReadOnlyCollection<PriceList> SameKey(IEnumerable<PriceList> published, PriceList list)
        => published.Where(p => p.LegalEntityId == list.LegalEntityId && p.CountryCode == list.CountryCode).ToList();

    private async Task<IReadOnlyList<PriceList>> ReconcileAsync(Guid tenantId, IReadOnlyList<PriceList> lists, CancellationToken ct)
    {
        if (_reconciler is null || lists.All(l => l.OpenRound() is null))
        {
            return lists;
        }

        if (await _reconciler.ReconcileAsync(tenantId, PricingApprovalKinds.PriceList, lists, ct) == 0)
        {
            return lists;
        }

        var reloaded = new List<PriceList>(lists.Count);
        foreach (var list in lists)
        {
            reloaded.Add(await GetAsync(tenantId, list.Id, ct) ?? list);
        }

        return reloaded;
    }

    internal static (int Page, int Size) Paging(int? page, int? size)
        => (Math.Max(1, page ?? 1), Math.Clamp(size ?? PricingLimits.DefaultPageSize, 1, PricingLimits.MaxPageSize));
}
