using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory.Queries;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Traceability.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Handlers.QueryHandlers;

/// <summary>
/// getTransactions — immutable history of the caller's Tenant + LE, newest posting first.
/// <para>
/// MOD-0174 WP-0174-P4 (CT-D6, read-side resolution): a transaction the typed-lot migration linked to a registered lot is
/// ANSWERED under that lot — its <c>lotId</c> is the registered lot id (the stored document keeps its typed text: the ledger
/// is append-only and nothing here writes), and <c>?lotId=&lt;registered lot&gt;</c> returns the lot's own rows plus the
/// rows linked to it, merged in the contract order (posting date, then creation, newest first) and paged after the merge.
/// Without links the query is the unchanged MOD-0173 query.
/// </para>
/// </summary>
public sealed class GetTransactionsHandler : IRequestHandler<GetTransactionsQuery, Response<TransactionPageDto>>
{
    /// <summary>Upper bound of the rows one linked-lot query merges in memory (one lot's history).</summary>
    private const int MaxMergedRows = 10_000;

    private readonly IInventoryLedgerRepository _ledger;
    private readonly ILotMasterClient _lots;
    private readonly Common.IAuditActorNames _names;
    private readonly Common.ITenantContext _tenant;

    public GetTransactionsHandler(IInventoryLedgerRepository ledger, ILotMasterClient lots, Common.IAuditActorNames names, Common.ITenantContext tenant)
    {
        _ledger = ledger;
        _lots = lots;
        _names = names;
        _tenant = tenant;
    }

    public async Task<Response<TransactionPageDto>> Handle(GetTransactionsQuery request, CancellationToken cancellationToken)
    {
        DateTimeOffset? from = WireParsing.TryParseUtcDate(request.From, out var fromDay) ? fromDay : null;
        DateTimeOffset? toExclusive = WireParsing.TryParseUtcDate(request.To, out var toDay) ? toDay.AddDays(1) : null;
        var page = QueryParameterRules.Page(request.Page);
        var pageSize = QueryParameterRules.PageSize(request.PageSize);
        var itemId = QueryParameterRules.Uuid(request.ItemId);
        var lotFilter = QueryParameterRules.Text(request.LotId);
        var locationId = QueryParameterRules.Text(request.LocationId);

        IReadOnlyList<(Guid TransactionId, string TypedText)> linked = lotFilter is not null && LotPostingRules.IsRegisteredLotForm(lotFilter, out var lotId)
            ? await _lots.LinksOfLotAsync(lotId, cancellationToken)
            : [];

        IReadOnlyList<InventoryTransaction> items;
        long total;
        if (linked.Count == 0)
        {
            var result = await _ledger.QueryTransactionsAsync(
                new TransactionQuery(itemId, lotFilter, locationId, from, toExclusive, page, pageSize), cancellationToken);
            (items, total) = (result.Items, result.Total);
        }
        else
        {
            // The lot's own rows + the rows linked to it (each typed text read once, a row kept only when a link names it).
            var linkedIds = linked.Select(l => l.TransactionId).ToHashSet();
            var merged = new List<InventoryTransaction>();
            foreach (var text in linked.Select(l => l.TypedText).Prepend(lotFilter!).Distinct(StringComparer.Ordinal))
            {
                var rows = await _ledger.QueryTransactionsAsync(
                    new TransactionQuery(itemId, text, locationId, from, toExclusive, 1, MaxMergedRows), cancellationToken);
                merged.AddRange(text == lotFilter ? rows.Items : rows.Items.Where(t => linkedIds.Contains(t.Id)));
            }

            var ordered = merged.DistinctBy(t => t.Id).OrderByDescending(t => t.PostingDate).ThenByDescending(t => t.CreatedAt).ToList();
            (items, total) = (ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList(), ordered.Count);
        }

        // Answer a linked row under its registered lot: the in-memory copy read for this response is relabelled before
        // mapping (never written back — there is no write path here).
        var resolved = await _lots.LinkedLotIdsAsync(
            items.Where(t => t.LotId is not null && !LotPostingRules.IsRegisteredLotForm(t.LotId, out _)).Select(t => t.Id).ToList(),
            cancellationToken);
        foreach (var transaction in items)
        {
            if (resolved.TryGetValue(transaction.Id, out var registered))
            {
                transaction.LotId = registered;
            }
        }

        // INVENTORY 1.5.0 (WP-MVP1-NAMES1): the posting names of the WHOLE page in ONE read — the central audit queue's display name of
        // each row's posting correlation and posting user, this company only. A row without a correlation or a user asks for nothing.
        var names = await _names.NamesAsync(
            items.Where(t => t.CorrelationId != Guid.Empty && t.CreatedBy != Guid.Empty).Select(t => (t.CorrelationId, t.CreatedBy)).Distinct().ToList(),
            _tenant.LegalEntityId, cancellationToken);

        return Response<TransactionPageDto>.Success(new TransactionPageDto
        {
            Items = items.Select(t => InventoryMapping.ToTransactionItem(t, names)).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total
        });
    }
}
