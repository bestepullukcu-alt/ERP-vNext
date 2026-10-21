using System.Globalization;
using System.Text;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Domain.Recall.Trace;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Recall.Trace;

/// <summary>
/// The trace as the recall commands use it (pack §8.2–§8.3): runs the engine with the company's limits (KR-G settings, default 20 / 5000),
/// turns a trace into scope items (root + forward lots, their stock, their shipments — backward inputs are shown, never scoped or
/// blocked), reconciles a re-trace with the stored items (trace items follow the trace, manual items stay), and seals a trace into a
/// <see cref="RecallTraceSnapshot"/>.
/// </summary>
public sealed class RecallTraceService
{
    private readonly RecallTraceEngine _engine;
    private readonly IRecallRepository _repository;
    private readonly RecallSupport _support;

    public RecallTraceService(RecallTraceEngine engine, IRecallRepository repository, RecallSupport support)
    {
        _engine = engine;
        _repository = repository;
        _support = support;
    }

    public async Task<TraceResult?> RunAsync(Guid rootLotId, RecallTraceDirection direction, DateTimeOffset? asOf, CancellationToken ct)
    {
        var settings = await _repository.FindSettingsAsync(ct) ?? RecallSettingsRules.Defaults();
        var now = _support.Now();
        return await _engine.RunAsync(new TraceRequest(rootLotId, direction, asOf ?? now, settings.TraceMaxDepth, settings.TraceMaxNodes), now, ct);
    }

    public static string Canonical(TraceResult r) => TraceRules.Canonical(r.RootLotId, r.Direction, r.Truncated, r.Nodes, r.Edges);

    public RecallTraceSnapshot Snapshot(TraceResult r, RecallCase c, RecallSnapshotPurpose purpose, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = at,
        CaseId = c.Id,
        Revision = c.ScopeRevision,
        Purpose = purpose,
        RootLotId = r.RootLotId,
        Direction = r.Direction,
        CanonicalVersion = r.CanonicalVersion,
        Sha256 = r.Sha256,
        Canonical = Canonical(r),
        NodeKeys = r.Nodes.Select(n => n.NodeKey).ToList(),
        NodeCount = r.Nodes.Count,
        GapCount = r.Summary.GapCount,
        Truncated = r.Truncated,
        ComputedAtUtc = r.ComputedAtUtc,
        DurationMs = r.DurationMs,
        CreatedBy = _support.UserId
    };

    /// <summary>The scope items of a trace (pack §8.3 step 1): root + forward lot nodes, their Stock nodes, their Shipment nodes.</summary>
    public static List<RecallScopeItem> ScopeItems(TraceResult r, Guid caseId, int revision, Guid user, DateTimeOffset at)
    {
        var items = new List<RecallScopeItem>();
        foreach (var n in r.Nodes.Where(n => n.Side is TraceSide.Root or TraceSide.Forward))
        {
            RecallScopeItemKind? kind = TraceRules.IsLotKind(n.Kind) ? RecallScopeItemKind.Lot
                : n.Kind == TraceNodeKind.Stock ? RecallScopeItemKind.Stock
                : n.Kind == TraceNodeKind.Shipment ? RecallScopeItemKind.Shipment
                : null;
            if (kind is null || n.LotId is null || n.SkuId is null) continue;
            var document = n.Kind == TraceNodeKind.Shipment ? n.NodeKey[(n.NodeKey.IndexOf(':', "Shipment:".Length) + 1)..] : null;
            items.Add(new RecallScopeItem
            {
                Id = Guid.NewGuid(),
                CreatedAt = at,
                CaseId = caseId,
                Revision = revision,
                NodeKey = n.NodeKey,
                ParentKey = n.Side == TraceSide.Root ? null : n.ParentKey,
                Kind = kind.Value,
                IsRoot = n.Side == TraceSide.Root,
                LotId = n.LotId,
                LotNumber = n.LotNumber ?? string.Empty,
                ItemId = n.ItemId!.Value,
                SkuId = n.SkuId.Value,
                SkuLevel = n.SkuLevel!.Value,
                WarehouseId = n.WarehouseId,
                LocationId = n.LocationId,
                StockStatus = n.StockStatus,
                SourceDocumentId = document,
                CustomerRef = kind == RecallScopeItemKind.Shipment ? n.CustomerRef ?? "DOC:" + document : null,
                Quantity = kind == RecallScopeItemKind.Lot ? null : n.Quantity,
                BaseUomId = kind == RecallScopeItemKind.Lot ? null : n.BaseUomId,
                Included = true,
                Origin = RecallScopeItemOrigin.Trace,
                AddedBy = user,
                AddedAtUtc = at
            });
        }

        return items;
    }

    /// <summary>
    /// Pack §8.3 step 3 — a re-trace against the stored items (incl. soft-removed): a trace item still in the trace keeps its mark and takes
    /// the new facts; one that left the trace is soft-removed; a new node is added included and flagged "yeni"; a removed node that came
    /// back is restored included and "yeni"; MANUAL items are never touched.
    /// </summary>
    public static (List<RecallScopeItem> Inserts, List<RecallScopeItem> Replaces) Reconcile(
        IReadOnlyList<RecallScopeItem> stored, IReadOnlyList<RecallScopeItem> traced, DateTimeOffset at)
    {
        var byKey = stored.ToDictionary(i => i.NodeKey, StringComparer.Ordinal);
        var tracedKeys = traced.Select(t => t.NodeKey).ToHashSet(StringComparer.Ordinal);
        var inserts = new List<RecallScopeItem>();
        var replaces = new List<RecallScopeItem>();
        foreach (var t in traced)
        {
            if (!byKey.TryGetValue(t.NodeKey, out var s))
            {
                t.IsNew = true;
                inserts.Add(t);
                continue;
            }

            if (s.Origin == RecallScopeItemOrigin.Manual && !s.IsDeleted) continue; // a hand entry with the same key wins (R1)
            var restored = s.IsDeleted;
            s.IsDeleted = false;
            s.Origin = RecallScopeItemOrigin.Trace;
            s.IsNew = restored;
            if (restored)
            {
                s.Included = true;
                s.ExclusionReason = null;
            }

            s.ParentKey = t.ParentKey;
            s.Quantity = t.Quantity;
            s.BaseUomId = t.BaseUomId;
            s.CustomerRef = t.CustomerRef;
            s.UpdatedAt = at;
            replaces.Add(s);
        }

        foreach (var s in stored.Where(s => !tracedKeys.Contains(s.NodeKey) && !s.IsDeleted))
        {
            if (s.Origin == RecallScopeItemOrigin.Trace && !s.IsRoot)
            {
                s.IsDeleted = true;
                s.DeletedAt = at;
                s.IsNew = false;
            }
            else if (s.IsNew)
            {
                s.IsNew = false;
            }
            else
            {
                continue;
            }

            s.UpdatedAt = at;
            replaces.Add(s);
        }

        return (inserts, replaces);
    }

    // ── wire ──────────────────────────────────────────────────────────────────────────────────────────────

    public static RecallTraceDto Dto(TraceResult r) => new(
        r.RootLotId, r.RootLotNumber, r.Direction, RecallSupport.Wire(r.AsOfUtc), RecallSupport.Wire(r.ComputedAtUtc), r.DurationMs, r.Truncated,
        new RecallTraceLimitsDto(r.MaxDepth, r.MaxNodes),
        r.Nodes.Select(Node).ToList(),
        r.Edges.OrderBy(e => e.From, StringComparer.Ordinal).ThenBy(e => e.To, StringComparer.Ordinal)
            .Select(e => new RecallTraceEdgeDto(e.From, e.To, Kind: "Genealogy", e.TransformationReference, e.QuantityConsumed, e.UomId, e.Cycle)).ToList(),
        r.Gaps.Select(Gap).ToList(),
        new RecallTraceSummaryDto(r.Summary.FinishedGoodLots, r.Summary.Stock.Select(Qty).ToList(), r.Summary.Shipped.Select(Qty).ToList(),
            r.Summary.InTransitShipments, r.Summary.GapCount, r.Summary.OtherCompanyActivityCount),
        r.CanonicalVersion, r.Sha256);

    public static RecallTraceNodeDto Node(TraceNode n) => new(
        n.NodeKey, n.ParentKey, n.Level, n.Side.ToString(), n.Kind.ToString(),
        n.LotId is null ? null : new RecallLotRefDto(n.LotId, n.LotNumber ?? string.Empty),
        n.SkuId is null ? null : new RecallItemRefDto(n.ItemId!.Value.ToString("D"), n.SkuId.Value.ToString("D"), WireNames.ToWire(n.SkuLevel!.Value)),
        n.Untracked, n.WarehouseId, n.LocationId, n.StockStatus is { } s ? WireNames.ToWire(s) : null, n.Quantity, n.BaseUomId,
        n.Movement is { } m ? new RecallTraceMovementDto(m.TransactionId, m.TransactionNumber, m.MovementType, RecallSupport.Wire(m.PostingDate), m.SourceModule, m.SourceDocumentId) : null,
        n.Shipment is { } sh ? new RecallTraceShipmentDto(sh.Read.ToString(), sh.Status, RecallSupport.Wire(sh.ActualDeliverAt), sh.ShipToReference) : null,
        n.CustomerRef, n.Serials, n.GapCode?.ToString(), n.Count, n.Text);

    /// <summary>A gap row: its code, node and text, plus the company's own movement facts — OTHER_COMPANY_ACTIVITY carries the count only (CT-R8).</summary>
    public static RecallTraceGapDto Gap(TraceNode n) => new(
        n.GapCode!.Value.ToString(), n.NodeKey, n.Text ?? string.Empty, n.Movement?.TransactionId, n.Movement?.TransactionNumber, n.SkuId?.ToString("D"),
        n.Quantity, n.BaseUomId, n.Movement is { } m ? RecallSupport.Wire(m.PostingDate) : null, n.Count);

    private static RecallTraceQuantityDto Qty(TraceQuantity q) => new(q.SkuId.ToString("D"), q.BaseUomId, q.Quantity, q.Count);

    public static RecallSnapshotRefDto? SnapshotRef(RecallTraceSnapshot? s) => s is null
        ? null
        : new RecallSnapshotRefDto(s.Id.ToString("D"), s.Revision, s.Purpose.ToString(), s.Sha256, s.CanonicalVersion, s.NodeCount, s.GapCount, s.Truncated,
            RecallSupport.Wire(s.ComputedAtUtc));

    /// <summary>Pack §8.2 CSV — the rule-10 node order, UTF-8 with BOM, ';' separated (mockup L:235 columns + unit and gap).</summary>
    public static string Csv(TraceResult r)
    {
        static string Cell(string? v) => "\"" + (v ?? string.Empty).Replace("\"", "\"\"") + "\"";
        const string Eol = "\r\n"; // RFC 4180 — never Environment.NewLine (the same trace gives the same bytes on every host)
        var sb = new StringBuilder("﻿");
        sb.Append(string.Join(';', new[] { "Düğüm türü", "SKU", "Lot", "Depo / Müşteri", "Miktar", "Birim", "Hareket / Sevkiyat no", "Tarih", "Boşluk" }.Select(Cell))).Append(Eol);
        foreach (var n in r.Nodes)
        {
            var place = n.Kind == TraceNodeKind.Shipment ? n.CustomerRef : n.WarehouseId;
            sb.Append(string.Join(';', new[]
            {
                n.Untracked ? "Input (Takip dışı)" : n.Kind.ToString(), n.SkuId?.ToString("D"), n.LotNumber, place,
                n.Quantity is { } q ? q.ToString(CultureInfo.InvariantCulture) : null, n.BaseUomId,
                n.Movement?.SourceDocumentId ?? n.Movement?.TransactionNumber, n.Movement is { } m ? RecallSupport.WireDate(m.PostingDate) : null, n.GapCode?.ToString()
            }.Select(Cell))).Append(Eol);
        }

        return sb.ToString();
    }
}
