using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;

namespace Diten.InventoryService.Domain.Recall.Repositories;

public sealed record RecallCaseQuery(
    IReadOnlyCollection<RecallCaseStatus>? Statuses,
    int? Class,
    Guid? SkuId,
    DateTimeOffset? OpenedFrom,
    string? Search,
    bool SortByNumber,
    int Page,
    int PageSize);

public sealed record RecallCasePage(IReadOnlyList<RecallCase> Items, long Total, IReadOnlyDictionary<RecallCaseStatus, long> StatusCounts);

public sealed record RecallSettingsHistoryPage(IReadOnlyList<RecallSettingsHistoryEntry> Items, long Total);

public sealed record RecallEventPage(IReadOnlyList<RecallCaseEvent> Items, long Total);

/// <summary>A write refused by a unique index (a create key, the active-root key, an event key, a scope node key).</summary>
public sealed class RecallDuplicateException(string index) : Exception($"Duplicate on {index}.")
{
    public string Index { get; } = index;
}

/// <summary>An active lot block as <c>getLotRecallStatus</c> sees it — tenant-wide (CT-R7), with the company of its case.</summary>
public sealed record RecallLotBlockView(string LotId, Guid CaseId, string CaseNumber, Guid? CaseLegalEntityId, RecallCaseStatus? CaseStatus);

/// <summary>
/// MOD-0177 records (pack §3). Every member is bound to the caller's resolved Tenant + LegalEntity — there is no overload without the
/// scope, so a company's settings or case does not exist for another company (403 / 404 decided by the caller). The ONE deliberate
/// exception is <see cref="ActiveLotBlocksAsync"/>: a recalled lot is recalled in every company of the tenant (CT-R7), so it is
/// bound to the tenant only. Changes go through <see cref="InTransactionAsync{T}"/>: one Mongo transaction, all or nothing. Nothing
/// is ever deleted.
/// </summary>
public interface IRecallRepository
{
    Task<RecallSettings?> FindSettingsAsync(CancellationToken cancellationToken = default);

    Task<RecallSettingsHistoryPage> ListSettingsHistoryAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>The caller's earlier settings save with this idempotency key (its audit row), if any.</summary>
    Task<RecallAuditRecord?> FindSettingsSaveAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<RecallCase?> FindCaseAsync(Guid caseId, CancellationToken cancellationToken = default);

    Task<RecallCase?> FindCaseByCreateKeyAsync(Guid openedBy, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<RecallCase?> FindActiveCaseByRootAsync(string activeRootKey, CancellationToken cancellationToken = default);

    Task<RecallCasePage> ListCasesAsync(RecallCaseQuery query, CancellationToken cancellationToken = default);

    /// <summary>V23 pattern: the id names a case of ANOTHER company of the caller's tenant (→ 403, not 404).</summary>
    Task<bool> ExistsInAnotherCompanyAsync(Guid caseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecallScopeItem>> ScopeItemsAsync(Guid caseId, int revision, CancellationToken cancellationToken = default);

    /// <summary>R2: the items of a revision INCLUDING those a re-trace removed (soft-removed, so a node that comes back is restored).</summary>
    Task<IReadOnlyList<RecallScopeItem>> ScopeItemsWithRemovedAsync(Guid caseId, int revision, CancellationToken cancellationToken = default);

    /// <summary>R2: one trace snapshot of the caller's company.</summary>
    Task<RecallTraceSnapshot?> FindSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken = default);

    /// <summary>Scope items of many cases at once (list rows: affected quantity per SKU + unit).</summary>
    Task<IReadOnlyList<RecallScopeItem>> ScopeItemsOfCasesAsync(IReadOnlyCollection<Guid> caseIds, CancellationToken cancellationToken = default);

    Task<RecallEventPage> EventsAsync(Guid caseId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecallCaseEvent>> AllEventsAsync(Guid caseId, CancellationToken cancellationToken = default);

    /// <summary>The caller's earlier event on this case with this idempotency key, if any.</summary>
    Task<RecallCaseEvent?> FindEventByKeyAsync(Guid caseId, Guid actor, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecallCustomerNotification>> NotificationsAsync(Guid caseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecallConsumptionDeclaration>> ConsumptionsAsync(Guid caseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecallSegregationConfirmation>> SegregationsAsync(Guid caseId, CancellationToken cancellationToken = default);

    Task<RecallCustomerNotification?> FindNotificationByKeyAsync(Guid recordedBy, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<RecallConsumptionDeclaration?> FindConsumptionByKeyAsync(Guid recordedBy, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<RecallSegregationConfirmation?> FindSegregationByKeyAsync(Guid confirmedBy, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>CT-R7 — the ACTIVE blocks of a lot in EVERY company of the caller's tenant (tenant-bound only, by design).</summary>
    Task<IReadOnlyList<RecallLotBlockView>> ActiveLotBlocksAsync(string lotId, CancellationToken cancellationToken = default);

    // ── R3 ──

    /// <summary>The blocks this case wrote (active or released), company-scoped.</summary>
    Task<IReadOnlyList<RecallLotBlock>> LotBlocksOfCaseAsync(Guid caseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecallQuarantineRequest>> QuarantineRequestsAsync(Guid caseId, CancellationToken cancellationToken = default);

    Task<RecallQuarantineRequest?> FindQuarantineRequestAsync(Guid requestId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecallReturnLink>> ReturnLinksAsync(Guid caseId, CancellationToken cancellationToken = default);

    /// <summary>The link of a transaction in this company, whatever its case (one case per return).</summary>
    Task<RecallReturnLink?> FindReturnLinkByTransactionAsync(string transactionId, CancellationToken cancellationToken = default);

    Task<RecallReturnLink?> FindReturnLinkByKeyAsync(Guid linkedBy, string idempotencyKey, CancellationToken cancellationToken = default);

    // ── R7b: the evidence pack (pack §4.13) — the company's export log and the stored bytes ──

    Task<RecallEvidenceExport?> FindEvidenceExportByKeyAsync(Guid exportedBy, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<RecallEvidenceExport?> FindEvidenceExportAsync(Guid exportId, CancellationToken cancellationToken = default);

    Task<RecallEvidenceFile?> FindEvidenceFileAsync(Guid exportId, CancellationToken cancellationToken = default);

    /// <summary>The export log of the company, newest first.</summary>
    Task<(IReadOnlyList<RecallEvidenceExport> Items, long Total)> ListEvidenceExportsAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>The export exists in another company of the tenant (the controller answers 403 instead of 404).</summary>
    Task<bool> EvidenceExportExistsInAnotherCompanyAsync(Guid exportId, CancellationToken cancellationToken = default);

    /// <summary>R4 (CT-R16): one prepared settings change of this company (the MOD-0022 subject <c>recall.settings-change</c>).</summary>
    Task<RecallSettingsChange?> FindSettingsChangeAsync(Guid changeId, CancellationToken cancellationToken = default);

    /// <summary>R4: the caller's prepared change with this idempotency key, if any (replay).</summary>
    Task<RecallSettingsChange?> FindSettingsChangeByKeyAsync(Guid setBy, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>R4: the caller's NEWEST prepared settings change in this company, if any.</summary>
    Task<RecallSettingsChange?> LatestSettingsChangeAsync(Guid setBy, CancellationToken cancellationToken = default);

    /// <summary>R4 (pack §8.8 "never used before"): a case decision (its event) or a settings save (its history) of this company already carries the id.</summary>
    Task<bool> SignatureUsedAsync(string signatureId, CancellationToken cancellationToken = default);

    Task<T> InTransactionAsync<T>(Func<IRecallWriteSession, CancellationToken, Task<T>> work, CancellationToken cancellationToken = default);

    // ── R6 (Hazırlık Paneli, pack §8.9) ──

    /// <summary>The company's OPEN cases (status not Closed / Cancelled), newest opened first.</summary>
    Task<IReadOnlyList<RecallCase>> OpenCasesAsync(CancellationToken cancellationToken = default);

    /// <summary>The return links of these cases in the company (the panel's "Geri dönen" per SKU + unit).</summary>
    Task<IReadOnlyList<RecallReturnLink>> ReturnLinksOfCasesAsync(IReadOnlyCollection<Guid> caseIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// The writes of one MOD-0177 transaction. History rows, case events, records and audit rows are INSERT-ONLY by shape: this session has
/// an insert for each and nothing that updates, replaces or deletes them (architecture-tested). The mutable heads are the settings, the
/// case, a scope item (Draft marks) and a lot block (release) — each version- or state-guarded.
/// </summary>
public interface IRecallWriteSession
{
    /// <summary>Next <c>RC-{yyyy}-{nnnn}</c> of the tenant (atomic counter).</summary>
    Task<string> NextCaseNumberAsync(int year, CancellationToken cancellationToken = default);

    /// <summary>Next <c>RC-{yyyy}-D{nnn}</c> of the tenant — the drill-run counter (runs are R5).</summary>
    Task<string> NextDrillNumberAsync(int year, CancellationToken cancellationToken = default);

    /// <summary>version 0 → insert the company's first row; otherwise replace the row of <paramref name="expectedVersion"/>.</summary>
    Task<bool> SaveSettingsAsync(RecallSettings settings, int expectedVersion, CancellationToken cancellationToken = default);

    Task InsertSettingsHistoryAsync(IReadOnlyList<RecallSettingsHistoryEntry> entries, CancellationToken cancellationToken = default);

    Task InsertCaseAsync(RecallCase recallCase, CancellationToken cancellationToken = default);

    Task<bool> ReplaceCaseAsync(RecallCase recallCase, int expectedVersion, CancellationToken cancellationToken = default);

    Task InsertScopeItemsAsync(IReadOnlyList<RecallScopeItem> items, CancellationToken cancellationToken = default);

    /// <summary>A Draft mark (include / exclude + reason) of one item.</summary>
    Task ReplaceScopeItemAsync(RecallScopeItem item, CancellationToken cancellationToken = default);

    Task InsertLotBlocksAsync(IReadOnlyList<RecallLotBlock> blocks, CancellationToken cancellationToken = default);

    /// <summary>Releases every active block of the case (closure).</summary>
    Task<long> ReleaseLotBlocksAsync(Guid caseId, DateTimeOffset at, string reason, CancellationToken cancellationToken = default);

    Task InsertEventAsync(RecallCaseEvent caseEvent, CancellationToken cancellationToken = default);

    Task InsertNotificationAsync(RecallCustomerNotification notification, CancellationToken cancellationToken = default);

    Task InsertConsumptionAsync(RecallConsumptionDeclaration declaration, CancellationToken cancellationToken = default);

    Task InsertSegregationAsync(RecallSegregationConfirmation confirmation, CancellationToken cancellationToken = default);

    Task InsertAuditAsync(RecallAuditRecord record, CancellationToken cancellationToken = default);

    /// <summary>R2: a trace snapshot (insert-only).</summary>
    Task InsertSnapshotAsync(RecallTraceSnapshot snapshot, CancellationToken cancellationToken = default);

    // ── R3 ──

    Task InsertQuarantineRequestsAsync(IReadOnlyList<RecallQuarantineRequest> requests, CancellationToken cancellationToken = default);

    /// <summary>Version-guarded (the request head is mutable: status, attempts, holds); false = stale.</summary>
    Task<bool> ReplaceQuarantineRequestAsync(RecallQuarantineRequest request, int expectedVersion, CancellationToken cancellationToken = default);

    Task InsertReturnLinkAsync(RecallReturnLink link, CancellationToken cancellationToken = default);

    /// <summary>R7b — the export log row and its file bytes (insert-only, one transaction with the audit row).</summary>
    Task InsertEvidenceExportAsync(RecallEvidenceExport export, RecallEvidenceFile file, CancellationToken cancellationToken = default);

    /// <summary>R4: a prepared settings change (insert-only).</summary>
    Task InsertSettingsChangeAsync(RecallSettingsChange change, CancellationToken cancellationToken = default);
}
