using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Traceability.Services;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Domain.Traceability.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Traceability.Handlers.QueryHandlers;

/// <summary>
/// listManualLotGroups (pack §11.5, K-D): the tenant's migratable typed groups with their K-D suggestion
/// (<see cref="ManualLotMatcher"/>) and totals; <c>view=pending</c> = groups still unlinked (all of them until the commit —
/// decisions are page state, ASSUMPTION-0174-11), <c>view=all</c> the same set (a linked group is no longer a typed group).
/// Once the commit ran, <c>completed</c> carries its result and the list is empty.
/// </summary>
public sealed class ListManualLotGroupsHandler : IRequestHandler<ListManualLotGroupsQuery, Response<ManualLotGroupPageDto>>
{
    private readonly IManualLotMigrationRepository _store;
    private readonly ManualLotGroupReader _groups;

    public ListManualLotGroupsHandler(IManualLotMigrationRepository store, ManualLotGroupReader groups)
    {
        _store = store;
        _groups = groups;
    }

    public async Task<Response<ManualLotGroupPageDto>> Handle(ListManualLotGroupsQuery request, CancellationToken cancellationToken)
    {
        if (request.View is not (null or "all" or "pending"))
            return TraceWriteSupport.Invalid<ManualLotGroupPageDto>("view must be all or pending.");
        if (!TraceQueryParsing.TryPaging(request.Page, request.PageSize, out var page, out var pageSize))
            return TraceWriteSupport.Invalid<ManualLotGroupPageDto>("page must be ≥ 1 and pageSize 1-200.");

        ManualLotGroupSnapshot snapshot;
        try
        {
            snapshot = await _groups.ReadAsync(cancellationToken);
        }
        catch (ProductMasterUnavailableException)
        {
            return TraceWriteSupport.Fail<ManualLotGroupPageDto>(TraceErrorCodes.ProductMasterUnavailable,
                "PRODUCT-MASTER could not say which items are lot-controlled; nothing was read.", 503);
        }

        var groups = snapshot.Groups;
        var lots = await _store.LotsOfSkusAsync(groups.Select(g => (g.Group.SkuId, g.Group.SkuLevel)).Distinct().ToList(), cancellationToken);
        var suggestions = ManualLotMatcher.Suggest(groups.Select(g => g.Group).ToList(), lots);
        var migration = await _store.GetAsync(cancellationToken);

        return Response<ManualLotGroupPageDto>.Success(new ManualLotGroupPageDto
        {
            Items = groups.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(g => TraceMigrationMapping.ToGroup(g, suggestions[g.Group.GroupId])).ToList(),
            Total = groups.Count,
            Totals = new ManualLotGroupTotalsDto { Groups = groups.Count, Transactions = groups.Sum(g => g.Group.TransactionCount) },
            Completed = migration is { ManualLotEntryClosed: true } ? TraceMigrationMapping.ToResult(migration) : null
        });
    }
}

/// <summary>getMovementLotLinks — the links of the caller's company, by transaction and / or lot (CT-D6 read side).</summary>
public sealed class GetMovementLotLinksHandler : IRequestHandler<GetMovementLotLinksQuery, Response<MovementLotLinksDto>>
{
    private const int MaxLinks = 1000;

    private readonly IManualLotMigrationRepository _store;

    public GetMovementLotLinksHandler(IManualLotMigrationRepository store) => _store = store;

    public async Task<Response<MovementLotLinksDto>> Handle(GetMovementLotLinksQuery request, CancellationToken cancellationToken)
    {
        string? transactionId = null;
        if (request.TransactionId is not null)
        {
            if (!TraceWriteSupport.TryParseId(request.TransactionId, out var tx))
                return TraceWriteSupport.Invalid<MovementLotLinksDto>("transactionId must be a UUID.");
            transactionId = TraceMapping.Id(tx);
        }

        Guid? lotId = null;
        if (request.LotId is not null)
        {
            if (!TraceWriteSupport.TryParseId(request.LotId, out var lot))
                return TraceWriteSupport.Invalid<MovementLotLinksDto>("lotId must be a UUID.");
            lotId = lot;
        }

        var links = await _store.LinksAsync(transactionId, lotId, MaxLinks, cancellationToken);
        return Response<MovementLotLinksDto>.Success(new MovementLotLinksDto
        {
            Items = links.Select(l => new MovementLotLinkDto
            {
                TransactionId = l.TransactionId,
                TypedText = l.TypedText,
                LotId = TraceMapping.Id(l.LotId),
                MigrationId = TraceMapping.Id(l.MigrationId)
            }).ToList()
        });
    }
}

/// <summary>getManualLotMigrationStatus (1.2.0, CT K4-1) — read with <c>trace.lots.read</c>.</summary>
public sealed class GetManualLotMigrationStatusHandler : IRequestHandler<GetManualLotMigrationStatusQuery, Response<ManualLotMigrationStatusDto>>
{
    private readonly IManualLotMigrationRepository _store;

    public GetManualLotMigrationStatusHandler(IManualLotMigrationRepository store) => _store = store;

    public async Task<Response<ManualLotMigrationStatusDto>> Handle(GetManualLotMigrationStatusQuery request, CancellationToken cancellationToken)
    {
        var migration = await _store.GetAsync(cancellationToken);
        var closed = migration is { ManualLotEntryClosed: true };
        return Response<ManualLotMigrationStatusDto>.Success(new ManualLotMigrationStatusDto
        {
            ManualLotEntryClosed = closed,
            CompletedAt = closed ? TraceMapping.Wire(migration!.CompletedAt) : null
        });
    }
}
