using System.Globalization;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Enums;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Persistence.Traceability;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Expiry;

/// <summary>
/// MOD-0176 F2 — the expiry hooks of the posting path (pack §8.3, §8.3.4; CT-F1 / CT-F2 / CT-F3 / CT-F5 / CT-F10). Replaces the ledger's
/// <see cref="AllowAllExpiryPostingPolicy"/>. Request-scoped: a refusal decided by either check is kept for
/// <see cref="FlushRefusalAuditAsync"/>, which writes its audit row after the posting was refused.
/// <list type="bullet">
/// <item><b>Issue</b> (GOODS_ISSUE_SALES / GOODS_ISSUE_PROD from AVAILABLE, <see cref="ExpiryHookScope"/>): the FEFO order of the same company +
/// warehouse + SKU (<see cref="FefoOrderRule"/>); not compliant (<see cref="FefoIssueRule"/>) → mode Warn: post + FEFO_BYPASS; RequireReason
/// (default): reason ≥ 10 → post + FEFO_BYPASS, else 422 <c>FEFO_BYPASS_REASON_REQUIRED</c>; Block: 422 <c>FEFO_ORDER_VIOLATION</c>. Every issue
/// under the hook writes a <see cref="FefoIssueSnapshot"/> in the posting transaction.</item>
/// <item><b>Receipt</b> (GOODS_RECEIPT_PO, CT-F5): remaining shelf life below the SKU minimum (<see cref="MinRemainingShelfLifeRule"/>) → mode
/// Reject: 422 <c>LOT_BELOW_MIN_REMAINING_SHELF_LIFE</c>; AutoQuarantine (default) into AVAILABLE: the same 422 with
/// <c>details.requiredToStockStatus = QUARANTINE</c> — the ledger NEVER turns the requested status itself (CT-F1); into QUARANTINE /
/// QUALITY_INSPECTION / another held status, or mode Warn: post + BELOW_MIN_REMAINING. A null minimum or a lot without expiry: not applied.</item>
/// </list>
/// The in-transaction re-check is AUTHORITATIVE: it reads the candidate lots and balances in the posting transaction (the binding lends the
/// session) and writes the snapshot and the exception there.
/// </summary>
public sealed class ExpiryPostingPolicy : IExpiryPostingPolicy
{
    private const string Compliant = "Compliant";
    private const string Bypassed = "Bypassed";

    private readonly IMongoCollection<InventoryBalance> _balances;
    private readonly IMongoCollection<Lot> _lots;
    private readonly ExpiryHookWriter _writer;
    private readonly LedgerTransactionBinding _binding;
    private readonly ITenantContext _tenant;
    private readonly List<(string Action, Lot Lot, Dictionary<string, string?> Detail)> _pendingRefusals = [];

    public ExpiryPostingPolicy(IMongoDatabase database, ExpiryHookWriter writer, LedgerTransactionBinding binding, ITenantContext tenant)
    {
        _balances = database.GetCollection<InventoryBalance>(InventoryCollections.Balances);
        _lots = database.GetCollection<Lot>(TraceCollections.Lots);
        _writer = writer;
        _binding = binding;
        _tenant = tenant;
    }

    // ── pre-check (outside the transaction) ──────────────────────────────────────────────────────────────

    public async Task<ExpiryPreCheck> PreCheckAsync(ExpiryPostingRequest request, CancellationToken cancellationToken = default)
    {
        var kind = ExpiryHookScope.For(request.MovementType, request.FromStockStatus, request.ToStockStatus, request.SourceModule, hasRegisteredLot: true);
        if (kind == ExpiryHookKind.None) return ExpiryPreCheck.None;

        var policy = await _writer.PolicyAsync(null, cancellationToken);
        var input = new ExpiryPostingInput(kind, policy, request.FefoBypassReason, request.MinRemainingShelfLifeDays);
        var refusal = kind == ExpiryHookKind.Issue
            ? IssueRefusal(await FefoAsync(null, request.Lot, request.FromWarehouseId!, request.ItemId, request.SkuId, request.SkuLevel,
                request.PostingDate, input.MinRemainingShelfLifeDays, cancellationToken), request.Lot, request.FromWarehouseId!, policy, input.BypassReason)
            : ReceiptRefusal(request.Lot, request.ToStockStatus, request.ToWarehouseId, request.PostingDate, policy, input.MinRemainingShelfLifeDays);
        return refusal is null ? new ExpiryPreCheck(null, input) : new ExpiryPreCheck(refusal, null);
    }

    // ── re-check (inside the posting transaction) ────────────────────────────────────────────────────────

    public async Task<ExpiryReCheck> ReCheckAsync(InventoryTransaction transaction, Lot? lot, ExpiryPostingInput? input, CancellationToken cancellationToken = default)
    {
        if (lot is null) return ExpiryReCheck.None;
        var kind = ExpiryHookScope.For(transaction.MovementType, transaction.FromStockStatus, transaction.ToStockStatus, transaction.SourceModule, hasRegisteredLot: true);
        if (kind == ExpiryHookKind.None) return ExpiryReCheck.None;

        var session = _binding.Session ?? throw new InvalidOperationException("The expiry re-check runs only inside the posting transaction.");
        var policy = input?.Policy ?? await _writer.PolicyAsync(session, cancellationToken);
        var minimum = input?.MinRemainingShelfLifeDays;
        return kind == ExpiryHookKind.Issue
            ? await ReCheckIssueAsync(session, transaction, lot, policy, input?.BypassReason, minimum, cancellationToken)
            : await ReCheckReceiptAsync(session, transaction, lot, policy, minimum, cancellationToken);
    }

    private async Task<ExpiryReCheck> ReCheckIssueAsync(
        IClientSessionHandle session, InventoryTransaction t, Lot lot, ExpiryPolicy policy, string? reason, int? minimum, CancellationToken ct)
    {
        var warehouse = t.FromWarehouseId!;
        var decision = await FefoAsync(session, lot, warehouse, t.ItemId, t.SkuId, t.SkuLevel, t.PostingDate, minimum, ct);
        if (IssueRefusal(decision, lot, warehouse, policy, reason) is { } refusal) return new ExpiryReCheck(refusal, null);

        var checks = new ExpiryChecks { Fefo = decision.Compliant ? Compliant : Bypassed };
        Guid? exceptionId = null;
        if (!decision.Compliant)
        {
            var first = decision.FirstEligible!;
            exceptionId = await _writer.OpenAsync(session, new HookCase(
                ExpiryExceptionType.FefoBypass, $"FEFO_BYPASS:{t.Id:D}", lot, warehouse, t.BaseQuantity, t.BaseUomId, Source(t),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["firstEligibleLotId"] = first.Id.ToString("D"),
                    ["firstEligibleLotNumber"] = first.LotNumber,
                    ["firstEligibleExpiry"] = ExpiryHookWriter.Day(first.ExpiryDate),
                    ["bypassReason"] = ValidReason(reason) ?? "—",
                    ["mode"] = policy.FefoOutboundMode.ToString()
                },
                OriginUserId: t.CreatedBy, // CT-F10: the poster of the bypassing issue may not resolve it
                Description: ValidReason(reason)), policy, ct);
            checks.ExceptionIds.Add(exceptionId.Value.ToString("D"));
        }

        await _writer.SnapshotAsync(session, new FefoIssueSnapshot
        {
            Id = Guid.NewGuid(),
            CreatedAt = t.CreatedAt,
            TransactionId = t.Id.ToString("D"),
            WarehouseId = warehouse,
            ItemId = t.ItemId,
            SkuId = t.SkuId,
            SkuLevel = t.SkuLevel,
            MovementType = WireNames.ToWire(t.MovementType),
            IssuedLotId = lot.Id.ToString("D"),
            IssuedExpiry = lot.ExpiryDate,
            FirstEligibleLotId = decision.FirstEligible?.Id.ToString("D"),
            FirstEligibleExpiry = decision.FirstEligible?.ExpiryDate,
            Compliant = decision.Compliant,
            Bypassed = !decision.Compliant,
            BypassReason = decision.Compliant ? null : ValidReason(reason),
            ExceptionId = exceptionId,
            PostedAtUtc = _writer.Now(),
            PostingDate = t.PostingDate,
            PostedBy = t.CreatedBy
        }, ct);
        return new ExpiryReCheck(null, checks);
    }

    private async Task<ExpiryReCheck> ReCheckReceiptAsync(
        IClientSessionHandle session, InventoryTransaction t, Lot lot, ExpiryPolicy policy, int? minimum, CancellationToken ct)
    {
        if (ReceiptRefusal(lot, t.ToStockStatus, t.ToWarehouseId, t.PostingDate, policy, minimum) is { } refusal) return new ExpiryReCheck(refusal, null);

        var check = MinRemainingShelfLifeRule.Evaluate(lot.ExpiryDate, minimum, t.PostingDate);
        var checks = new ExpiryChecks
        {
            MinRemaining = check.Outcome switch
            {
                MinRemainingOutcome.NotApplicable => "NotApplicable",
                MinRemainingOutcome.NotDefined => "NotDefined",
                MinRemainingOutcome.Ok => "Ok",
                _ => "Warned"
            }
        };
        if (check.Outcome == MinRemainingOutcome.Below)
        {
            // Posted below the minimum: into a held status (the 0175 intake opens the quarantine hold) or in mode Warn. System case (CT-F10).
            var id = await _writer.OpenAsync(session, new HookCase(
                ExpiryExceptionType.BelowMinRemaining, $"BELOW_MIN_REMAINING:{t.Id:D}", lot, t.ToWarehouseId, t.BaseQuantity, t.BaseUomId, Source(t),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["remainingDays"] = check.RemainingDays!.Value.ToString(CultureInfo.InvariantCulture),
                    ["requiredDays"] = check.RequiredDays!.Value.ToString(CultureInfo.InvariantCulture),
                    ["requestedStockStatus"] = t.ToStockStatus is { } to ? WireNames.ToWire(to) : "",
                    ["mode"] = policy.BelowMinRemainingMode.ToString()
                },
                OriginUserId: null,
                Description: null), policy, ct);
            checks.ExceptionIds.Add(id.ToString("D"));
        }

        return new ExpiryReCheck(null, checks);
    }

    // ── the rules applied (both checks) ──────────────────────────────────────────────────────────────────

    private ExpiryRefusal? IssueRefusal(FefoIssueDecision decision, Lot lot, string warehouseId, ExpiryPolicy policy, string? reason)
    {
        if (decision.Compliant || policy.FefoOutboundMode == FefoOutboundMode.Warn) return null;
        if (policy.FefoOutboundMode == FefoOutboundMode.RequireReason && ValidReason(reason) is not null) return null;

        var first = decision.FirstEligible!;
        var details = new Dictionary<string, object?>
        {
            ["firstEligibleLotId"] = first.Id.ToString("D"),
            ["firstEligibleLotNumber"] = first.LotNumber,
            ["firstEligibleExpiry"] = ExpiryHookWriter.Day(first.ExpiryDate),
            ["lotId"] = lot.Id.ToString("D"),
            ["warehouseId"] = warehouseId
        };
        var block = policy.FefoOutboundMode == FefoOutboundMode.Block;
        Remember(Application.Features.Expiry.ExpiryContract.AuditFefoRefused, lot, details, block ? "Block" : "RequireReason");
        return block
            ? new ExpiryRefusal(InventoryErrorCodes.FefoOrderViolation,
                $"FEFO: lot {first.LotNumber} (expiry {ExpiryHookWriter.Day(first.ExpiryDate)}) must be issued first; this company refuses an issue out of FEFO order.", details)
            : new ExpiryRefusal(InventoryErrorCodes.FefoBypassReasonRequired,
                $"FEFO: lot {first.LotNumber} (expiry {ExpiryHookWriter.Day(first.ExpiryDate)}) expires earlier; issuing another lot needs fefoBypassReason (10-1000 characters).", details);
    }

    private ExpiryRefusal? ReceiptRefusal(Lot lot, StockStatus? to, string? warehouseId, DateTimeOffset postingDate, ExpiryPolicy policy, int? minimum)
    {
        var check = MinRemainingShelfLifeRule.Evaluate(lot.ExpiryDate, minimum, postingDate);
        if (check.Outcome != MinRemainingOutcome.Below || policy.BelowMinRemainingMode == BelowMinRemainingMode.Warn) return null;
        if (policy.BelowMinRemainingMode == BelowMinRemainingMode.AutoQuarantine && to != StockStatus.Available) return null;

        var details = new Dictionary<string, object?>
        {
            ["remainingDays"] = check.RemainingDays,
            ["requiredDays"] = check.RequiredDays,
            ["lotId"] = lot.Id.ToString("D"),
            ["warehouseId"] = warehouseId
        };
        if (policy.BelowMinRemainingMode == BelowMinRemainingMode.AutoQuarantine)
        {
            details["requiredToStockStatus"] = WireNames.ToWire(StockStatus.Quarantine); // CT-F1: the caller resubmits into QUARANTINE
        }

        Remember(Application.Features.Expiry.ExpiryContract.AuditReceiptRefused, lot, details, policy.BelowMinRemainingMode.ToString());
        return new ExpiryRefusal(InventoryErrorCodes.LotBelowMinRemainingShelfLife,
            policy.BelowMinRemainingMode == BelowMinRemainingMode.AutoQuarantine
                ? $"The lot has {check.RemainingDays} days of shelf life left, below the minimum of {check.RequiredDays}: receive it into QUARANTINE (requiredToStockStatus)."
                : $"The lot has {check.RemainingDays} days of shelf life left, below the minimum of {check.RequiredDays}: this company refuses the receipt.",
            details);
    }

    private async Task<FefoIssueDecision> FefoAsync(
        IClientSessionHandle? session, Lot issued, string warehouseId, Guid itemId, Guid skuId, SkuLevel level, DateTimeOffset asOf, int? minimum,
        CancellationToken ct)
    {
        var b = Builders<InventoryBalance>.Filter;
        // Covered by the prefix of the unique balance dimension index (Tenant, LE, Item, Sku, SkuLevel, Warehouse, …) — CT-F13: no new index.
        var filter = b.Eq(x => x.TenantId, _tenant.TenantId) & b.Eq(x => x.LegalEntityId, _tenant.LegalEntityId) & b.Eq(x => x.IsDeleted, false)
                     & b.Eq(x => x.ItemId, itemId) & b.Eq(x => x.SkuId, skuId) & b.Eq(x => x.SkuLevel, level) & b.Eq(x => x.WarehouseId, warehouseId)
                     & b.Ne(x => x.LotId, null) & b.Gt(x => x.OnHand, 0m);
        var rows = session is null ? await _balances.Find(filter).ToListAsync(ct) : await _balances.Find(session, filter).ToListAsync(ct);
        var perLot = rows.GroupBy(r => r.LotId!, StringComparer.Ordinal)
            .Select(g => (LotId: g.Key, Available: g.Where(r => r.StockStatus == StockStatus.Available).Sum(r => r.OnHand),
                Held: g.Where(r => r.StockStatus != StockStatus.Available).Sum(r => r.OnHand)))
            .ToList();
        var ids = perLot.Select(p => Guid.TryParse(p.LotId, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToList();
        var lotFilter = Builders<Lot>.Filter.Eq(x => x.TenantId, _tenant.TenantId) & Builders<Lot>.Filter.In(x => x.Id, ids);
        var lots = (session is null ? await _lots.Find(lotFilter).ToListAsync(ct) : await _lots.Find(session, lotFilter).ToListAsync(ct))
            .ToDictionary(l => l.Id.ToString("D"), StringComparer.Ordinal);
        var candidates = perLot.Where(p => lots.ContainsKey(p.LotId)).Select(p => new FefoCandidate(lots[p.LotId], p.Available, p.Held)).ToList();
        return FefoIssueRule.Evaluate(issued, FefoOrderRule.Order(candidates, asOf, minimum));
    }

    // ── refusal audit (after the refused posting) ────────────────────────────────────────────────────────

    private void Remember(string action, Lot lot, Dictionary<string, object?> details, string mode)
    {
        var detail = details.ToDictionary(d => d.Key, d => d.Value is null ? null : Convert.ToString(d.Value, CultureInfo.InvariantCulture), StringComparer.Ordinal);
        detail["mode"] = mode;
        _pendingRefusals.Add((action, lot, detail));
    }

    public async Task FlushRefusalAuditAsync(CancellationToken cancellationToken = default)
    {
        // A pre-check refusal and its in-transaction twin are the same refusal: one row per action + lot.
        foreach (var refusal in _pendingRefusals.DistinctBy(r => (r.Action, r.Lot.Id)).ToList())
        {
            await _writer.AuditAsync(null, refusal.Action, "trace.lot", refusal.Lot.Id, refusal.Lot.LotNumber, _tenant.UserId, refusal.Detail, cancellationToken);
        }

        _pendingRefusals.Clear();
    }

    private static string? ValidReason(string? reason)
        => reason?.Trim() is { Length: >= ExpiryExceptionRules.MinReasonLength } r ? r : null;

    private static FefoSourceMovement Source(InventoryTransaction t) => new()
    {
        TransactionId = t.Id.ToString("D"),
        MovementType = WireNames.ToWire(t.MovementType),
        SourceModule = t.SourceModule,
        SourceDocumentId = t.SourceDocumentId
    };
}
