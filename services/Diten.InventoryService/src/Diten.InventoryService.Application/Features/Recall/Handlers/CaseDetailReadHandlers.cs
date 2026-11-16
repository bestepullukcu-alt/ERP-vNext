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
    private readonly RecallCustomerDisplay _customers;

    public GetRecallCaseCustomersHandler(IRecallRepository repository, IRecallTraceSource ledger, IShipmentReader shipments, RecallCustomerDisplay customers)
    {
        _repository = repository;
        _ledger = ledger;
        _shipments = shipments;
        _customers = customers;
    }

    public async Task<Response<IReadOnlyList<RecallCustomerRowDto>>> Handle(GetRecallCaseCustomersQuery request, CancellationToken ct)
    {
        if (!RecallCustomerDisplay.TryPage(request.CustomerPage, out var page))
        {
            return RecallSupport.Invalid<IReadOnlyList<RecallCustomerRowDto>>(RecallCustomerDisplay.PageError);
        }

        if (!RecallSupport.TryParseId(request.CaseId, out var caseId) || await _repository.FindCaseAsync(caseId, ct) is not { } c)
        {
            return RecallSupport.CaseNotFound<IReadOnlyList<RecallCustomerRowDto>>();
        }

        var shipments = RecallReads.Shipments(await _repository.ScopeItemsAsync(c.Id, c.ScopeRevision, ct));
        // MOD-0177-FU01 (§8.6): the CRM keys come from the stored items — their names are read NOW, side by side with the MVP-6 read
        var facts = RecallCustomerDisplay.Facts(shipments);
        var names = _customers.PageAsync(facts.Values, page, ct);
        var movements = await _ledger.SalesIssuesOfLotsAsync(shipments.Select(s => s.LotId).Distinct(StringComparer.Ordinal).ToList(), ct);
        var behind = shipments.ToDictionary(s => s.NodeKey, s => RecallReads.MovementsBehind(s, movements), StringComparer.Ordinal);
        var readable = shipments
            .Where(s => s.SourceDocumentId is not null && behind[s.NodeKey].Any(m => m.IsShipmentDocument))
            .Select(s => s.SourceDocumentId!).Distinct(StringComparer.Ordinal).ToList();
        var reads = readable.Count == 0 ? new Dictionary<string, TraceShipmentInfo>() : await _shipments.ReadAsync(readable, ct);
        var resolution = await names;

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
            var key = RecallCustomerDisplay.Of(facts, g.Key);
            // a CRM key is never a label (§8.7); a key without a CRM id keeps today's label
            var label = key.IsCrm
                ? null
                : lines.Select(l => l.ShipToReference).FirstOrDefault(t => !string.IsNullOrEmpty(t))
                  ?? (g.Key.StartsWith("DOC:", StringComparison.Ordinal)
                      ? lines.SelectMany(l => l.Movements).Select(m => m.Number).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? g.Key["DOC:".Length..]
                      : g.Key);
            // the distinguisher (§8.8): the first live ship-to and the first movement number, items in node-key order, movements in posting order
            var shipTo = lines.Select(l => l.ShipToReference).FirstOrDefault(t => RecallCustomerDisplay.Visible(t) is not null);
            var number = g.OrderBy(s => s.NodeKey, StringComparer.Ordinal)
                .SelectMany(s => behind[s.NodeKey].OrderBy(m => m.PostingDate).ThenBy(m => m.TransactionId, StringComparer.Ordinal))
                .Select(m => m.Number).FirstOrDefault(n => RecallCustomerDisplay.Visible(n) is not null);
            return new RecallCustomerRowDto(
                g.Key,
                label,
                lines,
                current is null ? null : RecallMapping.Notification(current),
                mine.Select(RecallMapping.Notification).ToList(),
                consumptions.Where(d => string.Equals(d.CustomerRef, g.Key, StringComparison.Ordinal)).Select(RecallMapping.Consumption).ToList(),
                RecallCustomerDisplay.Display(key, resolution, label, shipTo, number));
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
    private readonly RecallCustomerDisplay _customers;

    public GetRecallCaseEffectivenessHandler(IRecallRepository repository, RecallSubjectFacts facts, RecallSupport support, RecallCustomerDisplay customers)
    {
        _repository = repository;
        _facts = facts;
        _support = support;
        _customers = customers;
    }

    public async Task<Response<RecallEffectivenessDto>> Handle(GetRecallCaseEffectivenessQuery request, CancellationToken ct)
    {
        if (!RecallCustomerDisplay.TryPage(request.CustomerPage, out var page)) return RecallSupport.Invalid<RecallEffectivenessDto>(RecallCustomerDisplay.PageError);
        if (!RecallSupport.TryParseId(request.CaseId, out var caseId) || await _repository.FindCaseAsync(caseId, ct) is not { } c)
        {
            return RecallSupport.CaseNotFound<RecallEffectivenessDto>();
        }

        var result = c.Effectiveness ?? await _facts.LiveEffectivenessAsync(c, null, _support.Now(), ct);
        // MOD-0177-FU01 (§8.7, son bakış 04 F-2): the source and warning of a row's key are those of the case's current scope revision
        var facts = RecallCustomerDisplay.Facts(await _repository.ScopeItemsAsync(c.Id, c.ScopeRevision, ct));
        var keys = result.Rows.Where(r => r.CustomerRef is not null).Select(r => RecallCustomerDisplay.Of(facts, r.CustomerRef!)).ToList();
        var names = await _customers.PageAsync(keys, page, ct);
        return Response<RecallEffectivenessDto>.Success(RecallMapping.Effectiveness(result, RecallMapping.Customers(facts, names))
            with { Source = c.Effectiveness is null ? "Live" : "Stored" });
    }
}
