using Diten.ManufacturingService.Application.Common;
using Diten.ManufacturingService.Application.Features.Boms.Queries;
using Diten.ManufacturingService.Domain.Entities;
using Diten.ManufacturingService.Domain.Repositories;
using Diten.ManufacturingService.Domain.Rules;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ManufacturingService.Application.Features.Boms.Handlers.QueryHandlers;

public sealed class GetBomListHandler(IBomRepository repository, ITenantContext tenant)
    : IRequestHandler<GetBomListQuery, Response<BomListResponse>>
{
    public async Task<Response<BomListResponse>> Handle(GetBomListQuery request, CancellationToken ct)
    {
        var statuses = (request.Status ?? []).Select(Enum.Parse<BomStatus>).Distinct().ToList();
        var order = request.OrderBy is null ? BomListOrder.UpdatedAt : Validators.GetBomListValidator.OrderKeys[request.OrderBy];
        var descending = request.OrderBy is null ? request.OrderDir != "asc" : request.OrderDir == "desc";
        var filter = request.ExportRowCap is { } cap
            ? new BomListFilter(request.ItemId, statuses, request.Search, order, descending, 0, cap + 1)
            : new BomListFilter(request.ItemId, statuses, request.Search, order, descending, request.Start, request.Length);
        var page = await repository.ListAsync(tenant.TenantId, tenant.LegalEntityId, filter, ct);
        return Response<BomListResponse>.Success(new BomListResponse(page.Items.Select(b => b.ToListItem()).ToList(), page.Total, page.FilteredTotal));
    }
}

/// <summary>getBomVersion (frozen) — başka kiracı/LE'nin ya da silinmiş sürümün kimliği 404 <c>UNKNOWN_BOM</c>.</summary>
public sealed class GetBomVersionHandler(IBomRepository repository, ITenantContext tenant)
    : IRequestHandler<GetBomVersionQuery, Response<BomView>>
{
    public async Task<Response<BomView>> Handle(GetBomVersionQuery request, CancellationToken ct)
    {
        var bom = await repository.GetByIdAsync(tenant.TenantId, tenant.LegalEntityId, request.BomVersionId, ct);
        return bom is null
            ? Response<BomView>.Fail(BomErrorCodes.UnknownBom, 404)
            : Response<BomView>.Success(bom.ToView());
    }
}

/// <summary>getCurrentBom (frozen) — <c>asOfDate</c> o UTC gününün sonu (ASSUMPTION-BOM-05), yoksa şimdi.</summary>
public sealed class GetCurrentBomHandler(IBomRepository repository, ITenantContext tenant, TimeProvider clock)
    : IRequestHandler<GetCurrentBomQuery, Response<BomView>>
{
    public async Task<Response<BomView>> Handle(GetCurrentBomQuery request, CancellationToken ct)
    {
        var at = BomRules.ResolveAsOf(request.AsOfDate, clock.UtcNowMs());
        var bom = await repository.GetEffectiveAtAsync(tenant.TenantId, tenant.LegalEntityId, request.ItemId, at, ct);
        return bom is null
            ? Response<BomView>.Fail(BomErrorCodes.UnknownBom, 404)
            : Response<BomView>.Success(bom.ToView());
    }
}

/// <summary>explodeBom (frozen) — tek seviye ihtiyaç (ASSUMPTION-BOM-02); kalıcı hiçbir şey yazmaz.</summary>
public sealed class ExplodeBomHandler(IBomRepository repository, ITenantContext tenant, TimeProvider clock)
    : IRequestHandler<ExplodeBomQuery, Response<ExplodeBomResponse>>
{
    public async Task<Response<ExplodeBomResponse>> Handle(ExplodeBomQuery request, CancellationToken ct)
    {
        var body = request.Body;
        var at = BomRules.ResolveAsOf(body.AsOfDate, clock.UtcNowMs());
        var bom = await repository.GetEffectiveAtAsync(tenant.TenantId, tenant.LegalEntityId, body.ItemId, at, ct);
        if (bom is null)
        {
            return Response<ExplodeBomResponse>.Fail(BomErrorCodes.UnknownBom, 404);
        }

        var requirements = BomRules.Explode(bom, body.Quantity!)
            .Select(r => new BomRequirementView(r.ComponentItemId, r.RequiredQuantity, r.UomId))
            .ToList();
        return Response<ExplodeBomResponse>.Success(new ExplodeBomResponse(body.ItemId, body.Quantity!, requirements, bom.Id.ToString()));
    }
}

/// <summary>
/// Sürüm geçmişi — AUD-001 yol c koşul 3: kaydı görmeye yetkili kullanıcı izini kaydın kendi ekranında okur.
/// Önce sürümün bu kiracı/LE'de var olduğu doğrulanır; yoksa 404 (geçmiş üzerinden varlık sızmaz).
/// </summary>
public sealed class GetBomHistoryHandler(IBomRepository repository, IBomHistoryJournal journal, ITenantContext tenant)
    : IRequestHandler<GetBomHistoryQuery, Response<BomHistoryResponse>>
{
    public async Task<Response<BomHistoryResponse>> Handle(GetBomHistoryQuery request, CancellationToken ct)
    {
        var bom = await repository.GetByIdAsync(tenant.TenantId, tenant.LegalEntityId, request.BomVersionId, ct);
        if (bom is null)
        {
            return Response<BomHistoryResponse>.Fail(BomErrorCodes.UnknownBom, 404);
        }

        var entries = await journal.ListAsync(tenant.TenantId, tenant.LegalEntityId, request.BomVersionId, ct);
        return Response<BomHistoryResponse>.Success(new BomHistoryResponse(bom.Id.ToString(), entries.Select(e => e.ToView()).ToList()));
    }
}
