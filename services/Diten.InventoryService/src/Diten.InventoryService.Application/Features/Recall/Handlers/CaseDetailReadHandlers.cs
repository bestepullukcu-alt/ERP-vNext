using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Queries;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Trace;
using Diten.InventoryService.Domain.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Recall.Handlers;

/// <summary>
/// WP-0177-R7a getCaseCustomers — "Müşteriler ve sevkiyatlar" (pack §8.6 KR-E / KR-F, RECALL 1.6.0). One row per customer of the effectively
/// included Shipment items of the case's current scope revision (customerRef = shipToReference read at trace time, else <c>DOC:&lt;document&gt;</c>,
/// D-18). Each shipment carries the ledger movements that stand behind it (<see cref="IRecallTraceSource.SalesIssuesOfLotsAsync"/>: the caller's
/// company only, net of reversals) and, for MOD-0183 documents, the shipment re-read NOW (no cache; every read outcome is an answer, D-19
/// <c>actualDeliverAt</c>). Notifications (current = the latest not superseded) and the effective consumption declarations per customer.
/// </summary>
public sealed class GetRecallCaseCustomersHandler : IRequestHandler<GetRecallCaseCustomersQuery, Response<IReadOnlyList<RecallCustomerRowDto>>>
{
    private readonly IRecallRepository _repository;
    private readonly IRecallTraceSource _ledger;
    private readonly IShipmentReader _shipments;

    public GetRecallCaseCustomersHandler(IRecallRepository repository, IRecallTraceSource ledger, IShipmentReader shipments)
    {
        _repository = repository;
        _ledger = ledger;
        _shipments = shipments;
    }

    public async Task<Response<IReadOnlyList<RecallCustomerRowDto>>> Handle(GetRecallCaseCustomersQuery request, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(request.CaseId, out var caseId) || await _repository.FindCaseAsync(caseId, ct) is not { } c)
        {
            return RecallSupport.CaseNotFound<IReadOnlyList<RecallCustomerRowDto>>();
        }

        var shipments = RecallReads.Shipments(await _repository.ScopeItemsAsync(c.Id, c.ScopeRevision, ct));
        var movements = await _ledger.SalesIssuesOfLotsAsync(shipments.Select(s => s.LotId).Distinct(StringComparer.Ordinal).ToList(), ct);
        IReadOnlyList<TraceSalesIssue> Behind(RecallScopeItem s)
            => movements.Where(m => string.Equals(m.LotId, s.LotId, StringComparison.Ordinal)
                                    && string.Equals(m.DocumentId ?? "TXN-" + m.TransactionId, s.SourceDocumentId, StringComparison.Ordinal)).ToList();

        var behind = shipments.ToDictionary(s => s.NodeKey, Behind, StringComparer.Ordinal);
        var readable = shipments
            .Where(s => s.SourceDocumentId is not null && behind[s.NodeKey].Any(m => m.IsShipmentDocument))
            .Select(s => s.SourceDocumentId!).Distinct(StringComparer.Ordinal).ToList();
        var reads = readable.Count == 0 ? new Dictionary<string, TraceShipmentInfo>() : await _shipments.ReadAsync(readable, ct);

        var notifications = await _repository.NotificationsAsync(c.Id, ct);
        var superseded = notifications.Where(n => n.SupersedesId is not null).Select(n => n.SupersedesId!.Value).ToHashSet();
        var consumptions = RecallReads.Effective(await _repository.ConsumptionsAsync(c.Id, ct));

        RecallCaseCustomerShipmentDto Shipment(RecallScopeItem s)
        {
            var info = s.SourceDocumentId is { } doc && readable.Contains(doc, StringComparer.Ordinal)
                ? reads.TryGetValue(doc, out var r) ? r : new TraceShipmentInfo(ShipmentReadOutcome.Unavailable, null, null, null)
                : new TraceShipmentInfo(ShipmentReadOutcome.NotApplicable, null, null, null);
            return new RecallCaseCustomerShipmentDto(
                s.NodeKey,
                new RecallLotRefDto(s.LotId, s.LotNumber),
                new RecallItemRefDto(s.ItemId.ToString("D"), s.SkuId.ToString("D"), WireNames.ToWire(s.SkuLevel)),
                s.SourceDocumentId,
                s.Quantity ?? 0m,
                s.BaseUomId ?? string.Empty,
                behind[s.NodeKey].Select(m => new RecallCustomerMovementDto(m.TransactionId, m.Number, m.BaseQuantity, RecallSupport.Wire(m.PostingDate))).ToList(),
                info.Read.ToString(),
                info.Read == ShipmentReadOutcome.Ok ? info.Status : null,
                info.Read == ShipmentReadOutcome.Ok ? RecallSupport.Wire(info.ActualDeliverAt) : null,
                info.Read == ShipmentReadOutcome.Ok ? info.ShipToReference : null);
        }

        var rows = shipments.GroupBy(s => s.CustomerRef!, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g =>
        {
            var lines = g.OrderBy(s => s.NodeKey, StringComparer.Ordinal).Select(Shipment).ToList();
            var mine = notifications.Where(n => string.Equals(n.CustomerRef, g.Key, StringComparison.Ordinal)).ToList();
            var current = mine.Where(n => !superseded.Contains(n.Id)).OrderBy(n => n.RecordedAtUtc).LastOrDefault();
            var label = lines.Select(l => l.ShipToReference).FirstOrDefault(t => !string.IsNullOrEmpty(t))
                        ?? (g.Key.StartsWith("DOC:", StringComparison.Ordinal)
                            ? lines.SelectMany(l => l.Movements).Select(m => m.Number).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? g.Key["DOC:".Length..]
                            : g.Key);
            return new RecallCustomerRowDto(
                g.Key,
                label,
                lines,
                current is null ? null : RecallMapping.Notification(current),
                mine.Select(RecallMapping.Notification).ToList(),
                consumptions.Where(d => string.Equals(d.CustomerRef, g.Key, StringComparison.Ordinal)).Select(RecallMapping.Consumption).ToList());
        }).ToList();

        return Response<IReadOnlyList<RecallCustomerRowDto>>.Success(rows);
    }
}

/// <summary>
/// WP-0177-R7a getCaseEffectiveness — "Etkinlik kontrolü" (pack §8.6): the stored result once <c>completeEffectivenessCheck</c> ran
/// (<c>source: Stored</c>), else the LIVE result of <see cref="RecallSubjectFacts.LiveEffectivenessAsync"/> — the ONE computation the prepared
/// completion signs and the completion stores (<c>source: Live</c>). Per (customer, SKU, base unit); totals per SKU + unit, never across units (D-20).
/// </summary>
public sealed class GetRecallCaseEffectivenessHandler : IRequestHandler<GetRecallCaseEffectivenessQuery, Response<RecallEffectivenessDto>>
{
    private readonly IRecallRepository _repository;
    private readonly RecallSubjectFacts _facts;
    private readonly RecallSupport _support;

    public GetRecallCaseEffectivenessHandler(IRecallRepository repository, RecallSubjectFacts facts, RecallSupport support)
    {
        _repository = repository;
        _facts = facts;
        _support = support;
    }

    public async Task<Response<RecallEffectivenessDto>> Handle(GetRecallCaseEffectivenessQuery request, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(request.CaseId, out var caseId) || await _repository.FindCaseAsync(caseId, ct) is not { } c)
        {
            return RecallSupport.CaseNotFound<RecallEffectivenessDto>();
        }

        return Response<RecallEffectivenessDto>.Success(c.Effectiveness is { } stored
            ? RecallMapping.Effectiveness(stored) with { Source = "Stored" }
            : RecallMapping.Effectiveness(await _facts.LiveEffectivenessAsync(c, null, _support.Now(), ct)) with { Source = "Live" });
    }
}
