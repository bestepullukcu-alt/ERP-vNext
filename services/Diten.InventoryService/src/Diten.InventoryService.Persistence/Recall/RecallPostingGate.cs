using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Recall;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Persistence.Traceability;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Recall;

/// <summary>
/// MOD-0177 R3 — the recall exit block in the MOD-0173 posting path (pack §8.5, KR-C; CT-R4 / CT-R7 / CT-R11) and the "under recall?" read of
/// the FEFO order (CT-R12). Replaces the ledger's <see cref="AllowAllRecallPostingGate"/> / <see cref="NoRecallLotStatusReader"/>.
/// <list type="bullet">
/// <item>The ACTIVE blocks of a lot are read TENANT-wide (CT-R7: a recalled lot is recalled in every company) — by lot id, or for a typed lot
/// text by number within the same SKU (ASSUMPTION-0177-06).</item>
/// <item>The decision is <see cref="RecallExitRule"/> — by movement TYPE only. This class never reads the posting's <c>sourceModule</c>
/// (architecture-tested): caller data cannot open the block, and there is no reason / signature override.</item>
/// <item>The re-check reads with the posting session (the ledger's binding) — the authoritative decision on the committed blocks.</item>
/// <item>Each refusal is recorded OUTSIDE the posting transaction (the 0176 refusal-audit pattern): the case timeline <c>ExitRefused</c> (in
/// the case's company) and the audit row <c>recall.exit.refused</c> (in the posting company).</item>
/// </list>
/// </summary>
public sealed class RecallPostingGate : IRecallPostingGate, IRecallLotStatusReader
{
    public const string StagePreCheck = "pre-check";
    public const string StageReCheck = "re-check";

    private readonly IMongoCollection<RecallLotBlock> _blocks;
    private readonly IMongoCollection<RecallCaseEvent> _events;
    private readonly IMongoCollection<RecallAuditRecord> _audit;
    private readonly LedgerTransactionBinding _binding;
    private readonly ITenantContext _tenant;
    private readonly RecallSupport _support;

    public RecallPostingGate(IMongoDatabase database, LedgerTransactionBinding binding, ITenantContext tenant, RecallSupport support)
    {
        _blocks = database.GetCollection<RecallLotBlock>(RecallCollections.LotBlocks);
        _events = database.GetCollection<RecallCaseEvent>(RecallCollections.CaseEvents);
        _audit = database.GetCollection<RecallAuditRecord>(InventoryCollections.Audit);
        _binding = binding;
        _tenant = tenant;
        _support = support;
    }

    public async Task<RecallPostingRefusal?> PreCheckAsync(RecallPostingRequest request, CancellationToken cancellationToken = default)
    {
        if (!RecallExitRule.IsBlocked(request.MovementType)) return null;
        var blocks = request.Lot is { } lot
            ? await ByLotAsync(null, lot.Id.ToString("D"), cancellationToken)
            : await ByTypedLotAsync(null, request.LotId, request.SkuId, request.SkuLevel, cancellationToken);
        var block = RecallExitRule.Evaluate(request.MovementType, request.FromStockStatus, request.ToStockStatus, request.LotId, blocks);
        return block is null ? null : await RefuseAsync(block, request.LotId, request.Lot?.LotNumber, request.MovementType, request.IdempotencyKey, StagePreCheck, cancellationToken);
    }

    public async Task<RecallPostingRefusal?> ReCheckAsync(InventoryTransaction transaction, Lot? lot, CancellationToken cancellationToken = default)
    {
        if (!RecallExitRule.IsBlocked(transaction.MovementType) || transaction.LotId is null) return null;
        var session = _binding.Session ?? throw new InvalidOperationException("The recall re-check runs only inside the posting transaction.");
        var blocks = lot is not null
            ? await ByLotAsync(session, lot.Id.ToString("D"), cancellationToken)
            : await ByTypedLotAsync(session, transaction.LotId, transaction.SkuId, transaction.SkuLevel, cancellationToken);
        var block = RecallExitRule.Evaluate(transaction.MovementType, transaction.FromStockStatus, transaction.ToStockStatus, transaction.LotId, blocks);
        return block is null ? null : await RefuseAsync(block, transaction.LotId, lot?.LotNumber, transaction.MovementType, transaction.IdempotencyKey, StageReCheck, cancellationToken);
    }

    public async Task<IReadOnlySet<string>> UnderRecallAsync(IReadOnlyCollection<string> lotIds, CancellationToken cancellationToken = default)
    {
        if (lotIds.Count == 0) return new HashSet<string>(StringComparer.Ordinal);
        var f = Builders<RecallLotBlock>.Filter;
        var rows = await _blocks.Find(f.Eq(x => x.TenantId, _tenant.TenantId) & f.In(x => x.LotId, lotIds) & f.Eq(x => x.Active, true) & f.Eq(x => x.IsDeleted, false))
            .ToListAsync(cancellationToken);
        return rows.Select(b => b.LotId).ToHashSet(StringComparer.Ordinal);
    }

    // ── reads (tenant-wide, CT-R7) ───────────────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<RecallLotBlock>> ByLotAsync(IClientSessionHandle? session, string lotId, CancellationToken ct)
    {
        var f = Builders<RecallLotBlock>.Filter;
        var filter = f.Eq(x => x.TenantId, _tenant.TenantId) & f.Eq(x => x.LotId, lotId) & f.Eq(x => x.Active, true) & f.Eq(x => x.IsDeleted, false);
        return session is null ? await _blocks.Find(filter).ToListAsync(ct) : await _blocks.Find(session, filter).ToListAsync(ct);
    }

    private async Task<IReadOnlyList<RecallLotBlock>> ByTypedLotAsync(IClientSessionHandle? session, string typedLot, Guid skuId, SkuLevel skuLevel, CancellationToken ct)
    {
        var f = Builders<RecallLotBlock>.Filter;
        var filter = f.Eq(x => x.TenantId, _tenant.TenantId) & f.Eq(x => x.SkuId, (Guid?)skuId) & f.Eq(x => x.Active, true) & f.Eq(x => x.IsDeleted, false);
        var rows = session is null ? await _blocks.Find(filter).ToListAsync(ct) : await _blocks.Find(session, filter).ToListAsync(ct);
        return rows.Where(b => RecallExitRule.TypedMatches(typedLot, skuId, skuLevel, b)).ToList();
    }

    // ── the refusal (422 LOT_UNDER_RECALL) and its record ────────────────────────────────────────────────

    private async Task<RecallPostingRefusal> RefuseAsync(RecallLotBlock block, string lotId, string? lotNumber, MovementType type, string idempotencyKey, string stage,
        CancellationToken ct)
    {
        var number = lotNumber ?? block.LotNumber ?? lotId;
        var sameCompany = block.LegalEntityId == _tenant.LegalEntityId;
        var details = new Dictionary<string, object?>
        {
            ["lotId"] = lotId,
            ["lotNumber"] = number,
            // CT-R7: another company learns that the lot is recalled and the case number — never the case id (getLotRecallStatus rule)
            ["recallCaseId"] = sameCompany ? block.CaseId.ToString("D") : null,
            ["recallCaseNumber"] = block.CaseNumber,
            ["movementType"] = WireNames.ToWire(type)
        };

        var now = _support.Now();
        var refs = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["lotId"] = lotId, ["lotNumber"] = number, ["movementType"] = WireNames.ToWire(type), ["requestKey"] = idempotencyKey, ["stage"] = stage,
            ["postingCompany"] = sameCompany ? null : "other"
        };
        var caseEvent = _support.Event(block.CaseId, RecallCaseEventKind.ExitRefused, now, $"{WireNames.ToWire(type)} of lot {number} refused (exit block).", refs);
        caseEvent.TenantId = block.TenantId;
        caseEvent.LegalEntityId = block.LegalEntityId; // the case's timeline lives in the case's company
        var audit = _support.Audit(RecallContract.AuditExitRefused, RecallContract.SubjectCase, block.CaseId, block.CaseNumber, now, idempotencyKey, refs);
        audit.TenantId = _tenant.TenantId;
        audit.LegalEntityId = _tenant.LegalEntityId;

        // outside the posting transaction (no session): the refusal stands even though the posting is rolled back
        await _events.InsertOneAsync(caseEvent, cancellationToken: ct);
        await _audit.InsertOneAsync(audit, cancellationToken: ct);

        return new RecallPostingRefusal(InventoryErrorCodes.LotUnderRecall,
            $"The lot {number} is under recall {block.CaseNumber}: {WireNames.ToWire(type)} is not allowed (exit block — no reason or signature overrides it).",
            details);
    }
}
