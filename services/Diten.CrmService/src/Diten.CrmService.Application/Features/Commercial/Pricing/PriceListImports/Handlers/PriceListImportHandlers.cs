using System.Security.Cryptography;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceListImports.Commands;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceListImports.Queries;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceListImports.Validators;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists.Validators;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories.Commercial;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PriceListImports.Handlers;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156 §4.2: ① upload (the file is parsed in the request, never stored) → ② match each row against
/// product-master (SKU code; finished-good GTIN where MDM can search it — v1.2, §20.3 AÇIK-5) → ③ compare with the list in
/// force (else the last published) of the same (legal entity, country) → ④ preview → ⑤ convert ONLY the matched rows into a
/// draft list. Unmatched / Retired / duplicate / invalid-price rows stay in the preview and never reach a list. A row passes
/// the SAME price check a manually typed item does (EK-1).
/// </summary>
public sealed class UploadPriceListImportHandler : IRequestHandler<UploadPriceListImportCommand, Response<PriceListImportDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IPriceListImportRepository _imports;
    private readonly IPriceListRepository _lists;
    private readonly IPriceListItemRepository _items;
    private readonly IPricingLegalEntityDirectory _legalEntities;
    private readonly IPricingSkuDirectory _skus;
    private readonly IPriceListFileParser _parser;
    private readonly TimeProvider _clock;

    public UploadPriceListImportHandler(ITenantContext tenant, IActorContext actor, IPriceListImportRepository imports, IPriceListRepository lists,
        IPriceListItemRepository items, IPricingLegalEntityDirectory legalEntities, IPricingSkuDirectory skus, IPriceListFileParser parser,
        TimeProvider? clock = null)
    {
        _tenant = tenant;
        _actor = actor;
        _imports = imports;
        _lists = lists;
        _items = items;
        _legalEntities = legalEntities;
        _skus = skus;
        _parser = parser;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<PriceListImportDto>> Handle(UploadPriceListImportCommand request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PriceListImportDto>("Tenant context is required.");
        }

        var upload = request.Upload;
        if (UploadPriceListImportValidator.Check(upload) is { } problem)
        {
            return PricingCommon.Fail<PriceListImportDto>(problem.Code, problem.Message, 400);
        }

        var lookup = await _legalEntities.GetAsync(upload.LegalEntityId!.Value, ct);
        if (lookup.Outcome == PricingLookupOutcome.Unavailable)
        {
            return PricingCommon.MdmUnavailable<PriceListImportDto>();
        }

        if (lookup.LegalEntity is not { } le)
        {
            return PricingCommon.Fail<PriceListImportDto>(PricingErrors.LegalEntityInvalid, "The legal entity does not exist in this tenant.", 400);
        }

        var parsed = _parser.Parse(upload.FileName!, upload.Content!, upload.Columns!);
        if (!parsed.Ok)
        {
            return PricingCommon.Fail<PriceListImportDto>(PricingErrors.ImportInvalid, parsed.Error ?? "The file cannot be read.", 400);
        }

        if (parsed.Rows.Count == 0 || parsed.Rows.Count > PricingLimits.ImportMaxRows)
        {
            return PricingCommon.Fail<PriceListImportDto>(PricingErrors.ImportInvalid,
                $"The file has 1–{PricingLimits.ImportMaxRows} data rows (found {parsed.Rows.Count}).", 400);
        }

        // ② match — by code first, then by GTIN for the rows a code did not match
        var codes = parsed.Rows.Select(r => PricingCommon.Clean(r.SkuCode)?.ToUpperInvariant()).OfType<string>().Distinct().ToList();
        IReadOnlyDictionary<string, PricingSku>? byCode = codes.Count == 0 ? new Dictionary<string, PricingSku>() : await _skus.FindByCodesAsync(codes, ct);
        if (byCode is null)
        {
            return PricingCommon.MdmUnavailable<PriceListImportDto>();
        }

        var gtins = parsed.Rows.Where(r => PricingCommon.Clean(r.SkuCode) is not { } c || !byCode.ContainsKey(c.ToUpperInvariant()))
            .Select(r => Gtin(r.Gtin)).OfType<string>().Distinct().ToList();
        IReadOnlyDictionary<string, PricingSku>? byGtin = gtins.Count == 0 ? new Dictionary<string, PricingSku>() : await _skus.FindByGtinsAsync(gtins, ct);
        if (byGtin is null)
        {
            return PricingCommon.MdmUnavailable<PriceListImportDto>();
        }

        var import = new PriceListImport
        {
            TenantId = tenantId, Name = PricingCommon.Clean(upload.Name) ?? Path.GetFileNameWithoutExtension(upload.FileName!), LegalEntityId = le.Id,
            LegalEntityName = le.Name, LegalEntityCode = le.Code, CountryCode = le.CountryCode.Trim().ToUpperInvariant(),
            Currency = PricingCommon.NormalizeCurrency(upload.Currency), Kind = PricingCommon.Clean(upload.Kind)!.ToLowerInvariant(),
            ValidFrom = upload.ValidFrom!.Value, ValidTo = upload.ValidTo, FileName = Path.GetFileName(upload.FileName!),
            FileSha256 = Convert.ToHexString(SHA256.HashData(upload.Content!)).ToLowerInvariant(), Status = PriceListImportStatuses.Validated,
            TotalRows = parsed.Rows.Count, CreatedBy = _actor.ActorName, UpdatedBy = _actor.ActorName
        };
        if (import.Name.Length > PricingLimits.NameMax)
        {
            import.Name = import.Name[..PricingLimits.NameMax];
        }

        var rows = new List<PriceListImportRow>(parsed.Rows.Count);
        var seen = new HashSet<Guid>();
        foreach (var source in parsed.Rows)
        {
            var row = new PriceListImportRow
            {
                TenantId = tenantId, ImportId = import.Id, RowNo = source.RowNo, SourceCode = PricingCommon.Clean(source.SkuCode),
                SourceGtin = Gtin(source.Gtin), DepotPrice = PricingCommon.ParseNumber(source.DepotPrice),
                PharmacistPurchasePrice = PricingCommon.ParseNumber(source.PharmacistPurchasePrice), PublicPrice = PricingCommon.ParseNumber(source.PublicPrice),
                VatRate = PricingCommon.ParseNumber(source.VatRate)
            };
            var sku = row.SourceCode is { } code && byCode.TryGetValue(code.ToUpperInvariant(), out var c) ? c
                : row.SourceGtin is { } gtin && byGtin.TryGetValue(gtin, out var g) ? g : null;
            if (sku is not null)
            {
                row.SkuId = sku.SkuId;
                row.SkuLevel = sku.SkuLevel;
                row.SkuCode = sku.Code;
                row.SkuName = sku.Name;
            }

            var pricesUnreadable = (Present(source.DepotPrice) && row.DepotPrice is null)
                                   || (Present(source.PharmacistPurchasePrice) && row.PharmacistPurchasePrice is null)
                                   || (Present(source.PublicPrice) && row.PublicPrice is null)
                                   || (Present(source.VatRate) && row.VatRate is null);
            row.RowStatus = sku is null ? PriceListImportRowStatuses.Unmatched
                : sku.IsRetired ? PriceListImportRowStatuses.Retired
                : pricesUnreadable || PriceListItemValidator.CheckPrices(row.DepotPrice, row.PharmacistPurchasePrice, row.PublicPrice, row.VatRate) is not null
                    ? PriceListImportRowStatuses.InvalidPrice
                    : !seen.Add(sku.SkuId) ? PriceListImportRowStatuses.Duplicate
                    : PriceListImportRowStatuses.Matched;
            rows.Add(row);
        }

        // ③ diff against the list in force (else the last published) of the same (legal entity, country)
        var published = await _lists.ListPublishedAsync(tenantId, import.LegalEntityId, import.CountryCode, ct);
        var compared = PricingCommon.InForceOn(published, PricingCommon.Today(_clock))
                       ?? published.Where(l => l.IsPublished()).OrderByDescending(l => l.ValidFrom).FirstOrDefault();
        if (compared is not null)
        {
            import.ComparedPriceListId = compared.Id;
            var previous = (await _items.ListAllAsync(tenantId, compared.Id, ct)).Where(i => !i.IsDeleted && i.TenantId == tenantId)
                .GroupBy(i => i.SkuId).ToDictionary(gr => gr.Key, gr => gr.First());
            foreach (var row in rows.Where(r => r.RowStatus == PriceListImportRowStatuses.Matched))
            {
                if (!previous.TryGetValue(row.SkuId!.Value, out var before))
                {
                    row.DiffKind = PriceListDiffKinds.Added;
                    continue;
                }

                row.PreviousDepotPrice = before.DepotPrice;
                row.PreviousPharmacistPurchasePrice = before.PharmacistPurchasePrice;
                row.PreviousPublicPrice = before.PublicPrice;
                row.PreviousVatRate = before.VatRate;
                var vatChanged = before.VatRate != row.VatRate;
                row.DiffKind = before.DepotPrice == row.DepotPrice && before.PharmacistPurchasePrice == row.PharmacistPurchasePrice
                               && before.PublicPrice == row.PublicPrice && !vatChanged
                    ? PriceListDiffKinds.Unchanged
                    : PriceListDiffKinds.Changed;
                if (vatChanged)
                {
                    import.VatChanged++;
                }
            }

            var carried = rows.Where(r => r.RowStatus == PriceListImportRowStatuses.Matched).Select(r => r.SkuId!.Value).ToHashSet();
            var rowNo = rows.Count == 0 ? 0 : rows.Max(r => r.RowNo);
            foreach (var gone in previous.Values.Where(p => !carried.Contains(p.SkuId)).OrderBy(p => p.SkuCode, StringComparer.Ordinal))
            {
                rows.Add(new PriceListImportRow
                {
                    TenantId = tenantId, ImportId = import.Id, RowNo = ++rowNo, SkuId = gone.SkuId, SkuLevel = gone.SkuLevel, SkuCode = gone.SkuCode,
                    SkuName = gone.SkuName, RowStatus = PriceListImportRowStatuses.Removed, DiffKind = PriceListDiffKinds.Removed,
                    PreviousDepotPrice = gone.DepotPrice, PreviousPharmacistPurchasePrice = gone.PharmacistPurchasePrice,
                    PreviousPublicPrice = gone.PublicPrice, PreviousVatRate = gone.VatRate
                });
            }
        }
        else
        {
            foreach (var row in rows.Where(r => r.RowStatus == PriceListImportRowStatuses.Matched))
            {
                row.DiffKind = PriceListDiffKinds.Added;
            }
        }

        import.Matched = rows.Count(r => r.RowStatus == PriceListImportRowStatuses.Matched);
        import.Unmatched = rows.Count(r => r.RowStatus == PriceListImportRowStatuses.Unmatched);
        import.Retired = rows.Count(r => r.RowStatus == PriceListImportRowStatuses.Retired);
        import.Duplicate = rows.Count(r => r.RowStatus == PriceListImportRowStatuses.Duplicate);
        import.InvalidPrice = rows.Count(r => r.RowStatus == PriceListImportRowStatuses.InvalidPrice);
        import.Added = rows.Count(r => r.DiffKind == PriceListDiffKinds.Added);
        import.Changed = rows.Count(r => r.DiffKind == PriceListDiffKinds.Changed);
        import.Removed = rows.Count(r => r.RowStatus == PriceListImportRowStatuses.Removed);

        await _imports.InsertAsync(import, ct);
        await _imports.InsertRowsAsync(rows, ct);
        return Response<PriceListImportDto>.Success(PriceListImportMapping.ToDto(import), 201);
    }

    private static bool Present(string? value) => !string.IsNullOrWhiteSpace(value);

    /// <summary>A GTIN as digits only (spaces / dashes dropped); a GTIN-13 is left as written, MDM matches its own form.</summary>
    private static string? Gtin(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }
}

/// <summary>⑤ The matched rows become a DRAFT list (its previous version = the compared list, so the draft's diff is the
/// import's); the import is closed as <c>converted</c>.</summary>
public sealed class ConvertPriceListImportHandler : IRequestHandler<ConvertPriceListImportCommand, Response<PriceListDto>>
{
    private readonly PriceListCommandContext _c;
    private readonly IPriceListImportRepository _imports;

    public ConvertPriceListImportHandler(PriceListCommandContext context, IPriceListImportRepository imports)
    {
        _c = context;
        _imports = imports;
    }

    public async Task<Response<PriceListDto>> Handle(ConvertPriceListImportCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PriceListDto>("Tenant context is required.");
        }

        var import = await _imports.GetByIdAsync(tenantId, request.Id, ct);
        if (import is null || import.TenantId != tenantId || import.IsDeleted)
        {
            return PricingCommon.NotFound<PriceListDto>();
        }

        if (import.Status != PriceListImportStatuses.Validated)
        {
            return PricingCommon.Fail<PriceListDto>(PricingErrors.ImportNotOpen, "Only a validated import is converted (once).", 409);
        }

        var matched = (await _imports.ListRowsAsync(tenantId, import.Id, PriceListImportRowStatuses.Matched, ct))
            .Where(r => r.TenantId == tenantId && r.SkuId is not null).ToList();
        if (matched.Count == 0)
        {
            return PricingCommon.Fail<PriceListDto>(PricingErrors.ImportNothingMatched, "No row of the file matched a product; nothing to convert.", 409);
        }

        var compared = import.ComparedPriceListId is { } comparedId ? await _c.GetAsync(tenantId, comparedId, ct) : null;
        var actor = _c.Actor.ActorName;
        var list = new PriceList
        {
            TenantId = tenantId, Name = import.Name, LegalEntityId = import.LegalEntityId, LegalEntityName = import.LegalEntityName,
            LegalEntityCode = import.LegalEntityCode, CountryCode = import.CountryCode, Currency = import.Currency, Kind = import.Kind,
            ValidFrom = import.ValidFrom, ValidTo = import.ValidTo, Status = PriceListStatuses.Draft, SourceImportId = import.Id,
            PreviousPriceListId = compared?.Id, CreatedBy = actor, UpdatedBy = actor
        };
        if (compared is not null)
        {
            var (family, _) = await _c.Lists.ListAsync(tenantId, new PriceListFilter(compared.LegalEntityId, compared.CountryCode), 0, int.MaxValue, ct);
            list.FamilyId = compared.FamilyId;
            list.ListVersion = family.Where(l => l.FamilyId == compared.FamilyId).Select(l => l.ListVersion).DefaultIfEmpty(compared.ListVersion).Max() + 1;
        }
        else
        {
            list.FamilyId = list.Id;
        }

        var items = matched.Select(r => new PriceListItem
        {
            TenantId = tenantId, PriceListId = list.Id, SkuId = r.SkuId!.Value, SkuLevel = r.SkuLevel, SkuCode = r.SkuCode, SkuName = r.SkuName,
            Gtin = r.SourceGtin, DepotPrice = r.DepotPrice, PharmacistPurchasePrice = r.PharmacistPurchasePrice, PublicPrice = r.PublicPrice,
            VatRate = r.VatRate ?? 0m, CreatedBy = actor
        }).ToList();
        list.ItemCount = items.Count;

        // the import closes first (a second convert finds it converted: one draft per import)
        import.Status = PriceListImportStatuses.Converted;
        import.ConvertedPriceListId = list.Id;
        import.UpdatedBy = actor;
        import.UpdatedAt = DateTimeOffset.UtcNow;
        if (!await _imports.ReplaceAsync(import, import.Version, ct))
        {
            return PricingCommon.Stale<PriceListDto>();
        }

        await _c.Lists.InsertAsync(list, ct);
        await _c.Items.InsertManyAsync(items, ct);
        await _c.Log.WriteAsync(tenantId, PricingChangeEntityTypes.PriceList, list.Id, list.Version, list.FamilyId, PricingChangeActions.Imported, actor,
            PricingChangeLogWriter.Created(("sourceImportId", import.Id.ToString("D")), ("fileName", import.FileName), ("items", items.Count),
                ("added", import.Added), ("changed", import.Changed), ("removed", import.Removed), ("unmatched", import.Unmatched),
                ("retired", import.Retired)), ct);
        return PriceListCommandContext.Created(await _c.DtoAsync(list, ct));
    }
}

public sealed class DiscardPriceListImportHandler : IRequestHandler<DiscardPriceListImportCommand, Response<PriceListImportDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IPriceListImportRepository _imports;

    public DiscardPriceListImportHandler(ITenantContext tenant, IActorContext actor, IPriceListImportRepository imports)
    {
        _tenant = tenant;
        _actor = actor;
        _imports = imports;
    }

    public async Task<Response<PriceListImportDto>> Handle(DiscardPriceListImportCommand request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PriceListImportDto>("Tenant context is required.");
        }

        var import = await _imports.GetByIdAsync(tenantId, request.Id, ct);
        if (import is null || import.TenantId != tenantId || import.IsDeleted)
        {
            return PricingCommon.NotFound<PriceListImportDto>();
        }

        if (import.Status is not (PriceListImportStatuses.Uploaded or PriceListImportStatuses.Validated))
        {
            return PricingCommon.Fail<PriceListImportDto>(PricingErrors.ImportNotOpen, "A converted or discarded import cannot be discarded.", 409);
        }

        import.Status = PriceListImportStatuses.Discarded;
        import.UpdatedBy = _actor.ActorName;
        import.UpdatedAt = DateTimeOffset.UtcNow;
        return await _imports.ReplaceAsync(import, import.Version, ct)
            ? Response<PriceListImportDto>.Success(PriceListImportMapping.ToDto(import))
            : PricingCommon.Stale<PriceListImportDto>();
    }
}

public sealed class GetPriceListImportHandler : IRequestHandler<GetPriceListImportQuery, Response<PriceListImportPreviewDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IPriceListImportRepository _imports;

    public GetPriceListImportHandler(ITenantContext tenant, IPriceListImportRepository imports)
    {
        _tenant = tenant;
        _imports = imports;
    }

    public async Task<Response<PriceListImportPreviewDto>> Handle(GetPriceListImportQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PriceListImportPreviewDto>("Tenant context is required.");
        }

        var import = await _imports.GetByIdAsync(tenantId, request.Id, ct);
        if (import is null || import.TenantId != tenantId || import.IsDeleted)
        {
            return PricingCommon.NotFound<PriceListImportPreviewDto>();
        }

        var statuses = (request.RowStatus ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant()).ToList();
        if (statuses.Any(s => !PricingCommon.In(s, PriceListImportRowStatuses.All)))
        {
            return PricingCommon.Invalid<PriceListImportPreviewDto>("rowStatus is matched, unmatched, retired, duplicate, invalid-price or removed.");
        }

        var diff = PricingCommon.Clean(request.DiffKind)?.ToLowerInvariant();
        if (diff is not null && diff is not (PriceListDiffKinds.Added or PriceListDiffKinds.Changed or PriceListDiffKinds.Unchanged or PriceListDiffKinds.Removed))
        {
            return PricingCommon.Invalid<PriceListImportPreviewDto>("diffKind is added, changed, unchanged or removed.");
        }

        var (page, size) = (Math.Max(1, request.Page ?? 1), Math.Clamp(request.PageSize ?? PricingLimits.DefaultPageSize, 1, PricingLimits.MaxPageSize));
        var (rows, total) = await _imports.ListRowsAsync(tenantId, import.Id, statuses.Count == 0 ? null : statuses, diff, (page - 1) * size, size, ct);
        return Response<PriceListImportPreviewDto>.Success(new PriceListImportPreviewDto(PriceListImportMapping.ToDto(import),
            rows.Where(r => r.TenantId == tenantId).Select(PriceListImportMapping.ToDto).ToList(), total, page, size));
    }
}
