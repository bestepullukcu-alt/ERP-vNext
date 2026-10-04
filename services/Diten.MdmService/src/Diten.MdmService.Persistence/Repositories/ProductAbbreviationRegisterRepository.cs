using System.Collections.Concurrent;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ProductAbbreviationRegisterRepository : IProductAbbreviationRegisterRepository
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<ProductAbbreviationRegisterEntry> _collection;
    private readonly IMongoCollection<ProductAbbreviationHistoryEntry> _history;
    private readonly IMongoCollection<BsonDocument> _productSlots;
    private readonly IMongoCollection<ProductAbbreviationAllocationLedger> _ledger;
    private readonly Guid _tenantId;
    private readonly ILogger<ProductAbbreviationRegisterRepository> _logger;

    /// <summary>WP-MDM-ABB-LIST-01 EK 7 — the databases whose retired FIX2 index this process has already dealt with.</summary>
    private static readonly ConcurrentDictionary<string, byte> RetiredIndexChecked = new(StringComparer.Ordinal);

    /// <summary>FIX4 item 9 — the tenants (per database) whose pending rows this process has already given an order.</summary>
    private static readonly ConcurrentDictionary<string, byte> BackfilledTenants = new(StringComparer.Ordinal);

    public const string AlreadyDecidedByAnother = "ABBREVIATION_ALREADY_DECIDED_BY_ANOTHER";
    /// <summary>FIX3 item 6 — the server cannot run a transaction (a standalone mongod): every ABB write needs one.</summary>
    public const string TransactionsUnavailable = "PRODUCT_ABBREVIATION_TRANSACTIONS_UNAVAILABLE";
    public const string PersistenceUnavailable = "ABBREVIATION_PERSISTENCE_UNAVAILABLE";
    private const string ActiveProductIndexName = "ux_mdm_product_abbreviation_register_tenant_active_product";
    private const string Reclassify = "__RECLASSIFY__";
    public const string PendingRequestIndexName = "ix_mdm_product_abbreviation_register_tenant_pending_requests";
    public const string PendingRetirementIndexName = "ix_mdm_product_abbreviation_register_tenant_pending_retirements";
    /// <summary>FIX2's pending-order index over every row; FIX3 replaced it with the two partial ones (FIX4 item 5).</summary>
    public const string RetiredPendingOrderIndexName = "ix_mdm_product_abbreviation_register_tenant_pending_order";

    public ProductAbbreviationRegisterRepository(
        IMongoDatabase database,
        ITenantContext tenantContext,
        ILogger<ProductAbbreviationRegisterRepository>? logger = null)
    {
        _database = database;
        _logger = logger ?? NullLogger<ProductAbbreviationRegisterRepository>.Instance;
        _collection = database.GetCollection<ProductAbbreviationRegisterEntry>("mdm_product_abbreviation_register");
        _history = database.GetCollection<ProductAbbreviationHistoryEntry>(
            ProductAbbreviationHistoryRepository.CollectionName);
        _productSlots = database.GetCollection<BsonDocument>("mdm_product_abbreviation_product_slots");
        _ledger = database.GetCollection<ProductAbbreviationAllocationLedger>(
            ProductAbbreviationAllocationLedgerRepository.CollectionName);
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
        // FIX3 item 3: the ledger row is written by this repository's request transaction, so its uniqueness rules
        // must exist even where the ledger repository itself was never built.
        ProductAbbreviationAllocationLedgerRepository.EnsureIndexes(_ledger);
    }

    public async Task<IReadOnlyList<ProductAbbreviationRegisterEntry>> GetPendingWorkItemsPageAsync(
        ProductAbbreviationPendingCursor? after,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        // FIX3 item 9: pending work written before FIX2 has no stored order; it gets one (the moment it started waiting)
        // before the first page is read, so a cursor never meets a row it cannot place.
        if (after is null)
        {
            await BackfillPendingSinceOnceAsync(cancellationToken);
        }

        // FIX2 A3: the order work started waiting in (a stored integer), then the id as the tie-break — a stable,
        // meaningful order that a keyset cursor can resume exactly, with nothing skipped and nothing repeated.
        // FIX3 item 10: each branch names its own partial index, so a page never walks the tenant's decided rows.
        var position = after is null
            ? Builders<ProductAbbreviationRegisterEntry>.Filter.Empty
            : Builders<ProductAbbreviationRegisterEntry>.Filter.Or(
                Builders<ProductAbbreviationRegisterEntry>.Filter.Gt(x => x.PendingSinceUtcTicks, after.PendingSinceUtcTicks),
                Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.PendingSinceUtcTicks, after.PendingSinceUtcTicks)
                & Builders<ProductAbbreviationRegisterEntry>.Filter.Gt(x => x.Id, after.Id));
        var filter = Builders<ProductAbbreviationRegisterEntry>.Filter.Or(
            TenantFilter & RequestedWork & position,
            TenantFilter & RetirementWork & position);

        return await _collection.Find(filter)
            .SortBy(x => x.PendingSinceUtcTicks)
            .ThenBy(x => x.Id)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductAbbreviationRegisterEntry>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
        => ids.Count == 0
            ? []
            : await _collection
                .Find(TenantFilter & Builders<ProductAbbreviationRegisterEntry>.Filter.In(x => x.Id, ids))
                .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductAbbreviationRegisterEntry>> GetByGlobalProductIdAsync(
        Guid globalProductId,
        CancellationToken cancellationToken = default)
        => await _collection
            .Find(TenantFilter
                  & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.GlobalProductId, globalProductId))
            .SortBy(x => x.Id)
            .Limit(500)
            .ToListAsync(cancellationToken);

    public async Task<bool> HasOpenEntryForProductAsync(
        Guid globalProductId,
        Guid exceptEntryId,
        CancellationToken cancellationToken = default)
        => await _collection
            .Find(TenantFilter
                  & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.GlobalProductId, globalProductId)
                  & Builders<ProductAbbreviationRegisterEntry>.Filter.Ne(x => x.Id, exceptEntryId)
                  & Builders<ProductAbbreviationRegisterEntry>.Filter.In(
                      x => x.LifecycleStatus,
                      [ProductAbbreviationLifecycleStatus.ACTIVE, ProductAbbreviationLifecycleStatus.REQUESTED]))
            .Limit(1)
            .AnyAsync(cancellationToken);

    /// <summary>A request (an allocation or a correction) waiting for a decision.</summary>
    private static FilterDefinition<ProductAbbreviationRegisterEntry> RequestedWork
        => Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
            x => x.LifecycleStatus,
            ProductAbbreviationLifecycleStatus.REQUESTED);

    /// <summary>An active code whose retirement waits for a decision (the id is a string while it waits, null after).</summary>
    private static FilterDefinition<ProductAbbreviationRegisterEntry> RetirementWork
        => Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
               x => x.LifecycleStatus,
               ProductAbbreviationLifecycleStatus.ACTIVE)
           & Builders<ProductAbbreviationRegisterEntry>.Filter.Type(x => x.RetirementRequestId, BsonType.String);

    /// <summary>
    /// FIX3 item 9 / FIX4 (CT decision) — a pending row written before FIX2 has no PendingSinceUtcTicks. It is given the
    /// moment it started waiting: the retirement request's time for a waiting retirement, otherwise the request's time.
    /// Both are stored as [clock ticks, offset minutes] (BL-030), so UTC ticks = ticks − offset × ticks-per-minute.
    /// <list type="bullet">
    /// <item>Once per process per tenant (and database): marked only after a run that succeeded.</item>
    /// <item>Never the reason a read fails: a failure is logged and the page is read as it is.</item>
    /// <item>A row whose time is not in that shape is left as it is — one bad row is one row (FIX1 B3(b)), it never
    /// stops the others from getting their order.</item>
    /// <item>Only this tenant's rows, and only rows without the field.</item>
    /// </list>
    /// </summary>
    private async Task BackfillPendingSinceOnceAsync(CancellationToken cancellationToken)
    {
        var marker = $"{_database.DatabaseNamespace.DatabaseName}|{_tenantId:N}";
        if (BackfilledTenants.ContainsKey(marker))
        {
            return;
        }

        try
        {
            await BackfillPendingSinceAsync(cancellationToken);
            BackfilledTenants.TryAdd(marker, 0);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "mdm.product_abbreviation.pending_order_backfill_failed TenantId={TenantId} ExceptionType={ExceptionType}",
                _tenantId,
                exception.GetType().Name);
        }
    }

    private async Task BackfillPendingSinceAsync(CancellationToken cancellationToken)
    {
        // A stored time is [clock ticks, offset minutes]. Anything else — not an array, not two items, an item that is
        // not a number or does not fit a long — is left WITHOUT an order ($$REMOVE): never a failed update for every other
        // row, and never a guessed time (WP-MDM-ABB-LIST-01 EK 5).
        static BsonDocument AsLong(BsonValue input) => new("$convert", new BsonDocument
        {
            ["input"] = input,
            ["to"] = "long",
            ["onError"] = BsonNull.Value,
            ["onNull"] = BsonNull.Value
        });

        static BsonDocument UtcTicksOrNothing(string field) => new("$cond", new BsonArray
        {
            new BsonDocument("$and", new BsonArray
            {
                new BsonDocument("$isArray", field),
                new BsonDocument("$eq", new BsonArray
                {
                    new BsonDocument("$size", new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$isArray", field), field, new BsonArray()
                    })),
                    2
                })
            }),
            new BsonDocument("$let", new BsonDocument
            {
                ["vars"] = new BsonDocument
                {
                    ["ticks"] = AsLong(new BsonDocument("$arrayElemAt", new BsonArray { field, 0 })),
                    ["offset"] = AsLong(new BsonDocument("$arrayElemAt", new BsonArray { field, 1 }))
                },
                ["in"] = new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$and", new BsonArray
                    {
                        new BsonDocument("$isNumber", "$$ticks"),
                        new BsonDocument("$isNumber", "$$offset")
                    }),
                    new BsonDocument("$subtract", new BsonArray
                    {
                        "$$ticks",
                        new BsonDocument("$multiply", new BsonArray { "$$offset", TimeSpan.TicksPerMinute })
                    }),
                    "$$REMOVE"
                })
            }),
            "$$REMOVE"
        });

        const string retirementAt = "$" + nameof(ProductAbbreviationRegisterEntry.RetirementRequestedAtUtc);
        const string requestedAt = "$" + nameof(ProductAbbreviationRegisterEntry.RequestedAtUtc);
        const string status = "$" + nameof(ProductAbbreviationRegisterEntry.LifecycleStatus);
        var missing = new BsonDocument(nameof(ProductAbbreviationRegisterEntry.PendingSinceUtcTicks), BsonNull.Value);
        // By what the row waits for: a waiting retirement by the retirement's time ONLY (an unreadable one stays without
        // an order — never stamped with the far older request time), a request by the request's time.
        var stamp = new BsonDocument("$set", new BsonDocument(
            nameof(ProductAbbreviationRegisterEntry.PendingSinceUtcTicks),
            new BsonDocument("$switch", new BsonDocument
            {
                ["branches"] = new BsonArray
                {
                    new BsonDocument
                    {
                        ["case"] = new BsonDocument("$eq", new BsonArray { status, (int)ProductAbbreviationLifecycleStatus.ACTIVE }),
                        ["then"] = UtcTicksOrNothing(retirementAt)
                    },
                    new BsonDocument
                    {
                        ["case"] = new BsonDocument("$eq", new BsonArray { status, (int)ProductAbbreviationLifecycleStatus.REQUESTED }),
                        ["then"] = UtcTicksOrNothing(requestedAt)
                    }
                },
                ["default"] = "$$REMOVE"
            })));
        foreach (var work in new[] { RequestedWork, RetirementWork })
        {
            await _collection.UpdateManyAsync(
                TenantFilter & work & missing,
                PipelineDefinition<ProductAbbreviationRegisterEntry, ProductAbbreviationRegisterEntry>.Create(stamp),
                cancellationToken: cancellationToken);
        }
    }

    public async Task<ProductAbbreviationRegisterEntry?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ProductAbbreviationRegisterEntry? result = await _collection
            .Find(TenantFilter & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);
        return result;
    }

    public async Task<ProductAbbreviationRegisterEntry?> GetByAllocationIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ProductAbbreviationRegisterEntry? result = await _collection.Find(
                TenantFilter & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                    x => x.AllocationIdempotencyKey,
                    idempotencyKey))
            .FirstOrDefaultAsync(cancellationToken);
        return result;
    }

    public async Task<ProductAbbreviationRegisterEntry?> GetActiveByGlobalProductIdAsync(
        Guid globalProductId,
        CancellationToken cancellationToken = default)
    {
        ProductAbbreviationRegisterEntry? result = await _collection.Find(
                TenantFilter
                & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.GlobalProductId, globalProductId)
                & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                    x => x.LifecycleStatus,
                    ProductAbbreviationLifecycleStatus.ACTIVE))
            .FirstOrDefaultAsync(cancellationToken);
        return result;
    }

    public async Task<ProductAbbreviationRegisterEntry?> ResolveActiveAsync(
        string normalizedAbbreviation,
        CancellationToken cancellationToken = default)
    {
        ProductAbbreviationRegisterEntry? result = await _collection.Find(
                TenantFilter
                & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                    x => x.NormalizedAbbreviation,
                    normalizedAbbreviation)
                & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                    x => x.LifecycleStatus,
                    ProductAbbreviationLifecycleStatus.ACTIVE))
            .FirstOrDefaultAsync(cancellationToken);
        return result;
    }

    public async Task<bool> AppendAuditIntentIfAbsentAsync(
        Guid id,
        LocalAuditIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (id == Guid.Empty || intent.IntentId == Guid.Empty || intent.TenantId != _tenantId
            || intent.AggregateType != AuditAggregateType.ProductAbbreviation || intent.AggregateId != id
            || intent.SourceService != AuditIntentContract.SourceService
            || intent.ContractVersion != "mod-0290.audit-intent.v1"
            || AuditIntentTemporalStorage.Validate(intent) != AuditIntentTemporalStorageKind.Current)
        {
            return false;
        }

        var filter = Builders<ProductAbbreviationRegisterEntry>.Filter.And(
            TenantFilter,
            Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(item => item.Id, id),
            // FIX1 S1: an intent for a decision the entry has since moved past is still that decision's intent.
            Builders<ProductAbbreviationRegisterEntry>.Filter.Gte(item => item.Version, intent.PostVersion),
            Builders<ProductAbbreviationRegisterEntry>.Filter.Not(
                Builders<ProductAbbreviationRegisterEntry>.Filter.ElemMatch(
                    item => item.AuditIntents,
                    embedded => embedded.IntentId == intent.IntentId)),
            Builders<ProductAbbreviationRegisterEntry>.Filter.Not(
                Builders<ProductAbbreviationRegisterEntry>.Filter.ElemMatch(
                    item => item.AuditIntentReceipts, receipt => receipt.IntentId == intent.IntentId)));
        var result = await _collection.UpdateOneAsync(
            filter,
            Builders<ProductAbbreviationRegisterEntry>.Update.Push(item => item.AuditIntents, intent),
            cancellationToken: cancellationToken);
        if (result.ModifiedCount == 1)
        {
            return true;
        }

        var stored = await GetByIdAsync(id, cancellationToken);
        if (stored is null) return false;
        var existing = stored.AuditIntents.Where(x => x.IntentId == intent.IntentId).Take(2).ToArray();
        var receipts = stored.AuditIntentReceipts.Where(x => x.IntentId == intent.IntentId).Take(2).ToArray();
        if (existing.Length == 1 && receipts.Length == 0)
        {
            var original = existing[0];
            return original.TenantId == intent.TenantId && original.AggregateId == intent.AggregateId
                && original.AggregateType == intent.AggregateType && original.Operation == intent.Operation
                && original.SourceService == intent.SourceService && original.ContractVersion == intent.ContractVersion
                && original.PreVersion == intent.PreVersion && original.PostVersion == intent.PostVersion
                && original.ActorId == intent.ActorId && original.CorrelationId == intent.CorrelationId
                && original.CausationId == intent.CausationId && original.CommandId == intent.CommandId
                && original.Sequence == intent.Sequence && original.TimestampUtc == intent.TimestampUtc
                && original.EvidenceHash == intent.EvidenceHash && original.SnapshotReference == intent.SnapshotReference
                && original.IdempotencyKey == intent.IdempotencyKey;
        }
        if (existing.Length != 0 || receipts.Length != 1) return false;
        var receipt = receipts[0];
        return receipt.TenantId == intent.TenantId && receipt.SourceService == intent.SourceService
            && receipt.IdempotencyKey == intent.IdempotencyKey && receipt.EvidenceHash == intent.EvidenceHash
            && receipt.ContractVersion == intent.ContractVersion && !string.IsNullOrWhiteSpace(receipt.CentralAcknowledgement)
            && !string.IsNullOrWhiteSpace(receipt.CompactReceiptReference)
            && receipt.CentralIdempotencyKey == AuditIntentContract.BuildCentralIdempotencyKey(
                intent.TenantId, intent.IntentId, intent.ContractVersion);
    }

    public async Task<ProductAbbreviationRegisterWriteResult> ApproveCorrectionAsync(
        ProductAbbreviationCorrectionApproval approval,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(approval);
        var formerKey = approval.IdempotencyKey + ":former";
        var replacementKey = approval.IdempotencyKey + ":replacement";
        var former = await GetByIdAsync(approval.FormerEntryId, cancellationToken);
        var replacement = await GetByIdAsync(approval.ReplacementEntryId, cancellationToken);
        if (former is null || replacement is null || replacement.ReplacesEntryId != former.Id
            || former.GlobalProductId != replacement.GlobalProductId)
        {
            return new(false, replacement, "ABBREVIATION_CORRECTION_CONFLICT");
        }

        if (Applied(former, replacement, formerKey, replacementKey))
        {
            return MineOrAnother(replacement, approval.CheckerSubjectId);
        }

        // Only a correction that is STILL requested is approved: a cancelled, rejected or already active one never
        // retires the code it was meant to replace. FIX4 item 4: when another approver's decision stands, that is the
        // answer, whatever key this approval came with.
        if (replacement.LifecycleStatus != ProductAbbreviationLifecycleStatus.REQUESTED)
        {
            return NotPendingFor(replacement, approval.CheckerSubjectId);
        }

        // FIX2 A5: the code being replaced must be ACTIVE. The FIX1 path that "finished" a former entry recognised only
        // by a ':former' key suffix is gone — a client-chosen key could carry that suffix (measured: no such half-done
        // state exists in dev or test data; ABB was never released).
        if (former.LifecycleStatus != ProductAbbreviationLifecycleStatus.ACTIVE)
        {
            return new(false, replacement, "ABBREVIATION_CORRECTION_FORMER_NOT_ACTIVE");
        }

        if (replacement.Version != approval.ExpectedReplacementVersion
            || former.Version != approval.ExpectedFormerVersion)
        {
            return new(false, replacement, "CONCURRENCY_CONFLICT");
        }

        var formerPost = Decided(former, ProductAbbreviationLifecycleStatus.RETIRED, approval, formerKey);
        var replacementPost = Decided(replacement, ProductAbbreviationLifecycleStatus.ACTIVE, approval, replacementKey);
        var evidence = approval.BuildEvidence(formerPost, replacementPost);
        var histories = new[] { Stored(evidence.FormerHistory), Stored(evidence.ReplacementHistory) };

        // FIX3 item 5: the same transaction helper as every other ABB write — one place that opens the session, aborts,
        // and reads the stored state to say what happened.
        var outcome = await InTransactionAsync(
            async session =>
            {
                var retired = await _collection.UpdateOneAsync(
                    session,
                    ById(former.Id)
                    & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Version, approval.ExpectedFormerVersion)
                    & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                        x => x.LifecycleStatus,
                        ProductAbbreviationLifecycleStatus.ACTIVE),
                    DecisionUpdate(ProductAbbreviationLifecycleStatus.RETIRED, approval, formerKey)
                        .Set(x => x.RetirementRequestId, null)
                        .Set(x => x.RetirementRequestedByCanonicalSubjectId, null)
                        .Set(x => x.RetirementRequestedAtUtc, null)
                        .Set(x => x.PendingCorrectionEntryId, null)
                        .Push(x => x.AuditIntents, evidence.FormerIntent),
                    cancellationToken: cancellationToken);
                if (retired.ModifiedCount != 1)
                {
                    return new(false, replacement, Reclassify);
                }

                var activated = await _collection.UpdateOneAsync(
                    session,
                    ById(replacement.Id)
                    & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                        x => x.Version,
                        approval.ExpectedReplacementVersion)
                    & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                        x => x.LifecycleStatus,
                        ProductAbbreviationLifecycleStatus.REQUESTED),
                    DecisionUpdate(ProductAbbreviationLifecycleStatus.ACTIVE, approval, replacementKey)
                        .Push(x => x.AuditIntents, evidence.ReplacementIntent),
                    cancellationToken: cancellationToken);
                if (activated.ModifiedCount != 1)
                {
                    return new(false, replacement, Reclassify);
                }

                await _history.InsertManyAsync(session, histories, cancellationToken: cancellationToken);
                return null;
            },
            () => AfterAbortAsync(approval, formerKey, replacementKey, cancellationToken),
            "ABBREVIATION_CORRECTION_CONFLICT",
            cancellationToken);
        return outcome ?? new(true, await ReadBackAsync(replacement.Id, replacementPost, cancellationToken));
    }

    /// <summary>
    /// What the stored state says after a correction approval did not commit: applied under this key (by me or by
    /// another approver), no longer pending, moved on (409) — or null when nothing changed.
    /// </summary>
    private async Task<ProductAbbreviationRegisterWriteResult?> AfterAbortAsync(
        ProductAbbreviationCorrectionApproval approval,
        string formerKey,
        string replacementKey,
        CancellationToken cancellationToken)
    {
        var former = await GetByIdAsync(approval.FormerEntryId, cancellationToken);
        var replacement = await GetByIdAsync(approval.ReplacementEntryId, cancellationToken);
        if (former is not null && replacement is not null && Applied(former, replacement, formerKey, replacementKey))
        {
            return MineOrAnother(replacement, approval.CheckerSubjectId);
        }

        if (replacement is not null && replacement.LifecycleStatus != ProductAbbreviationLifecycleStatus.REQUESTED)
        {
            return NotPendingFor(replacement, approval.CheckerSubjectId);
        }

        return former is null || replacement is null
               || former.Version != approval.ExpectedFormerVersion
               || replacement.Version != approval.ExpectedReplacementVersion
               || former.LifecycleStatus != ProductAbbreviationLifecycleStatus.ACTIVE
            ? new(false, replacement, "CONCURRENCY_CONFLICT")
            : null;
    }

    private static bool Applied(
        ProductAbbreviationRegisterEntry former,
        ProductAbbreviationRegisterEntry replacement,
        string formerKey,
        string replacementKey)
        => replacement.LifecycleStatus == ProductAbbreviationLifecycleStatus.ACTIVE
           && replacement.LastDecisionIdempotencyKey == replacementKey
           && former.LifecycleStatus == ProductAbbreviationLifecycleStatus.RETIRED
           && former.LastDecisionIdempotencyKey == formerKey;

    private static ProductAbbreviationRegisterEntry Decided(
        ProductAbbreviationRegisterEntry entry,
        ProductAbbreviationLifecycleStatus status,
        ProductAbbreviationCorrectionApproval approval,
        string key)
    {
        var copy = BsonSerializer.Deserialize<ProductAbbreviationRegisterEntry>(entry.ToBsonDocument());
        copy.LifecycleStatus = status;
        copy.LastDecisionByCanonicalSubjectId = approval.CheckerSubjectId;
        copy.LastDecisionIdempotencyKey = key;
        copy.LastDecisionReason = approval.Reason;
        copy.LastDecisionAtUtc = approval.DecidedAtUtc;
        copy.UpdatedAt = approval.DecidedAtUtc;
        copy.Version = entry.Version + 1;
        if (status == ProductAbbreviationLifecycleStatus.RETIRED)
        {
            copy.RetirementRequestId = null;
            copy.RetirementRequestedByCanonicalSubjectId = null;
            copy.RetirementRequestedAtUtc = null;
            copy.PendingCorrectionEntryId = null;
        }

        return copy;
    }

    private static UpdateDefinition<ProductAbbreviationRegisterEntry> DecisionUpdate(
        ProductAbbreviationLifecycleStatus status,
        ProductAbbreviationCorrectionApproval approval,
        string key)
        => Builders<ProductAbbreviationRegisterEntry>.Update
            .Set(x => x.LifecycleStatus, status)
            .Set(x => x.LastDecisionByCanonicalSubjectId, approval.CheckerSubjectId)
            .Set(x => x.LastDecisionIdempotencyKey, key)
            .Set(x => x.LastDecisionReason, approval.Reason)
            .Set(x => x.LastDecisionAtUtc, approval.DecidedAtUtc)
            .Set(x => x.UpdatedAt, approval.DecidedAtUtc)
            .Inc(x => x.Version, 1);

    private FilterDefinition<ProductAbbreviationRegisterEntry> ById(Guid id)
        => TenantFilter & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Id, id);

    /// <summary>FIX4 item 4 — a request no longer waiting: another approver's decision, or simply not pending.</summary>
    private static ProductAbbreviationRegisterWriteResult NotPendingFor(ProductAbbreviationRegisterEntry entry, string actor)
        => ProductAbbreviationDecisionRules.DecidedByAnotherApprover(entry, actor)
            ? new(false, entry, AlreadyDecidedByAnother)
            : new(false, entry, "ABBREVIATION_NOT_PENDING");

    /// <summary>FIX4 item 4 — an approve or reject that finds another approver's decision standing, whatever its key.</summary>
    private static bool AnotherApproverDecided(ProductAbbreviationRegisterEntry entry, ProductAbbreviationDecision decision)
        => decision.Kind is ProductAbbreviationDecisionKind.Approve or ProductAbbreviationDecisionKind.Reject
           && entry.LifecycleStatus != ProductAbbreviationLifecycleStatus.REQUESTED
           && ProductAbbreviationDecisionRules.DecidedByAnotherApprover(entry, decision.ActorSubjectId);

    private static ProductAbbreviationRegisterWriteResult MineOrAnother(
        ProductAbbreviationRegisterEntry entry,
        string actor)
        => string.Equals(entry.LastDecisionByCanonicalSubjectId, actor, StringComparison.Ordinal)
            ? new(true, entry, IsReplay: true)
            : new(false, entry, AlreadyDecidedByAnother);

    private static bool IsActiveProductViolation(MongoException exception)
        => IsDuplicateKey(exception) && exception.Message.Contains(ActiveProductIndexName, StringComparison.Ordinal);

    // ══ FIX2 A1 — a decision, its history row and its audit intent: one transaction ══════════════════════════════

    public async Task<ProductAbbreviationRegisterWriteResult> DecideAsync(
        ProductAbbreviationDecision decision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var current = await GetByIdAsync(decision.EntryId, cancellationToken);
        if (current is null)
        {
            return new(false, null, "ABBREVIATION_NOT_FOUND");
        }

        var recognised = Recognise(current, decision);
        if (recognised is not null)
        {
            return recognised;
        }

        if (AnotherApproverDecided(current, decision))
        {
            return new(false, current, AlreadyDecidedByAnother);
        }

        if (current.Version != decision.ExpectedVersion
            || current.LifecycleStatus != ExpectedStatus(decision.Kind)
            || !RetirementStateHolds(current, decision))
        {
            return new(false, current, "CONCURRENCY_CONFLICT");
        }

        var post = Apply(current, decision);
        var evidence = decision.BuildEvidence(post);
        var history = Stored(evidence.History);
        var filter = ById(current.Id)
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Version, decision.ExpectedVersion)
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.LifecycleStatus, current.LifecycleStatus)
                     & RetirementFilter(current, decision);
        var outcome = await InTransactionAsync(
            async session =>
            {
                if (decision.Kind == ProductAbbreviationDecisionKind.RequestRetirement
                    && current.PendingCorrectionEntryId is { } claimed
                    && IsPendingCorrectionOf(
                        await _collection.Find(session, ById(claimed)).FirstOrDefaultAsync(cancellationToken),
                        current.Id))
                {
                    return new(false, current, "ABBREVIATION_CORRECTION_PENDING");
                }

                var updated = await _collection.UpdateOneAsync(
                    session,
                    filter,
                    DecisionUpdate(decision).Push(x => x.AuditIntents, evidence.Intent),
                    cancellationToken: cancellationToken);
                if (updated.ModifiedCount != 1)
                {
                    return new(false, current, Reclassify);
                }

                await _history.InsertOneAsync(session, history, cancellationToken: cancellationToken);
                return null;
            },
            async () =>
            {
                var after = await GetByIdAsync(decision.EntryId, cancellationToken);
                if (after is null)
                {
                    return new(false, null, "ABBREVIATION_NOT_FOUND");
                }

                if (Recognise(after, decision) is { } again)
                {
                    return again;
                }

                if (AnotherApproverDecided(after, decision))
                {
                    return new(false, after, AlreadyDecidedByAnother);
                }

                if (decision.Kind == ProductAbbreviationDecisionKind.RequestRetirement
                    && after.PendingCorrectionEntryId is { } pending
                    && IsPendingCorrectionOf(await GetByIdAsync(pending, cancellationToken), after.Id))
                {
                    return new(false, after, "ABBREVIATION_CORRECTION_PENDING");
                }

                return after.Version != current.Version
                       || after.LifecycleStatus != current.LifecycleStatus
                       || after.RetirementRequestId != current.RetirementRequestId
                       || after.PendingCorrectionEntryId != current.PendingCorrectionEntryId
                    ? new(false, after, "CONCURRENCY_CONFLICT")
                    : null;
            },
            "CONCURRENCY_CONFLICT",
            cancellationToken);
        return outcome ?? new(true, await ReadBackAsync(current.Id, post, cancellationToken));
    }

    /// <summary>
    /// The decision this key already wrote, if any: a success for the person who made it, a refusal for anyone else.
    /// Null when this key wrote nothing on the entry.
    /// </summary>
    private static ProductAbbreviationRegisterWriteResult? Recognise(
        ProductAbbreviationRegisterEntry entry,
        ProductAbbreviationDecision decision)
    {
        if (!string.Equals(entry.LastDecisionIdempotencyKey, decision.IdempotencyKey, StringComparison.Ordinal))
        {
            return null;
        }

        var (written, decider) = decision.Kind switch
        {
            ProductAbbreviationDecisionKind.Approve
                => (entry.LifecycleStatus == ProductAbbreviationLifecycleStatus.ACTIVE, entry.LastDecisionByCanonicalSubjectId),
            ProductAbbreviationDecisionKind.Reject
                => (entry.LifecycleStatus == ProductAbbreviationLifecycleStatus.REJECTED, entry.LastDecisionByCanonicalSubjectId),
            ProductAbbreviationDecisionKind.Cancel
                => (entry.LifecycleStatus == ProductAbbreviationLifecycleStatus.CANCELLED, entry.LastDecisionByCanonicalSubjectId),
            ProductAbbreviationDecisionKind.RequestRetirement
                => (entry.LifecycleStatus == ProductAbbreviationLifecycleStatus.ACTIVE
                    && entry.RetirementRequestId == decision.IdempotencyKey,
                    entry.RetirementRequestedByCanonicalSubjectId),
            ProductAbbreviationDecisionKind.ApproveRetirement
                => (entry.LifecycleStatus == ProductAbbreviationLifecycleStatus.RETIRED, entry.LastDecisionByCanonicalSubjectId),
            ProductAbbreviationDecisionKind.RejectRetirement
                => (entry.LifecycleStatus == ProductAbbreviationLifecycleStatus.ACTIVE && entry.RetirementRequestId is null,
                    entry.LastDecisionByCanonicalSubjectId),
            _ => (false, null)
        };
        if (!written)
        {
            return null;
        }

        return string.Equals(decider, decision.ActorSubjectId, StringComparison.Ordinal)
            ? new(true, entry, IsReplay: true)
            : new(false, entry, AlreadyDecidedByAnother);
    }

    private static ProductAbbreviationLifecycleStatus ExpectedStatus(ProductAbbreviationDecisionKind kind) => kind switch
    {
        ProductAbbreviationDecisionKind.Approve
            or ProductAbbreviationDecisionKind.Reject
            or ProductAbbreviationDecisionKind.Cancel => ProductAbbreviationLifecycleStatus.REQUESTED,
        _ => ProductAbbreviationLifecycleStatus.ACTIVE
    };

    private static bool RetirementStateHolds(ProductAbbreviationRegisterEntry entry, ProductAbbreviationDecision decision)
        => decision.Kind switch
        {
            ProductAbbreviationDecisionKind.RequestRetirement => entry.RetirementRequestId is null,
            ProductAbbreviationDecisionKind.ApproveRetirement or ProductAbbreviationDecisionKind.RejectRetirement
                => entry.RetirementRequestId is not null && entry.RetirementRequestId == decision.RetirementRequestId,
            _ => true
        };

    private static FilterDefinition<ProductAbbreviationRegisterEntry> RetirementFilter(
        ProductAbbreviationRegisterEntry current,
        ProductAbbreviationDecision decision) => decision.Kind switch
    {
        // The claim the request judged must still be the claim: a correction that claimed the code meanwhile wins.
        ProductAbbreviationDecisionKind.RequestRetirement
            => Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.RetirementRequestId, null)
               & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                   x => x.PendingCorrectionEntryId,
                   current.PendingCorrectionEntryId),
        ProductAbbreviationDecisionKind.ApproveRetirement or ProductAbbreviationDecisionKind.RejectRetirement
            => Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.RetirementRequestId, decision.RetirementRequestId),
        _ => Builders<ProductAbbreviationRegisterEntry>.Filter.Empty
    };

    private static UpdateDefinition<ProductAbbreviationRegisterEntry> DecisionUpdate(ProductAbbreviationDecision decision)
    {
        var update = Builders<ProductAbbreviationRegisterEntry>.Update
            .Set(x => x.LastDecisionIdempotencyKey, decision.IdempotencyKey)
            .Set(x => x.LastDecisionReason, decision.Reason)
            .Set(x => x.UpdatedAt, decision.DecidedAtUtc)
            .Inc(x => x.Version, 1);
        return decision.Kind switch
        {
            ProductAbbreviationDecisionKind.RequestRetirement => update
                .Set(x => x.RetirementRequestId, decision.IdempotencyKey)
                .Set(x => x.RetirementRequestedByCanonicalSubjectId, decision.ActorSubjectId)
                .Set(x => x.RetirementRequestedAtUtc, decision.DecidedAtUtc)
                .Set(x => x.PendingSinceUtcTicks, decision.DecidedAtUtc.UtcTicks),
            ProductAbbreviationDecisionKind.ApproveRetirement or ProductAbbreviationDecisionKind.RejectRetirement => update
                .Set(x => x.LifecycleStatus, decision.Kind == ProductAbbreviationDecisionKind.ApproveRetirement
                    ? ProductAbbreviationLifecycleStatus.RETIRED
                    : ProductAbbreviationLifecycleStatus.ACTIVE)
                .Set(x => x.RetirementRequestId, null)
                .Set(x => x.RetirementRequestedByCanonicalSubjectId, null)
                .Set(x => x.RetirementRequestedAtUtc, null)
                .Set(x => x.LastDecisionByCanonicalSubjectId, decision.ActorSubjectId)
                .Set(x => x.LastDecisionAtUtc, decision.DecidedAtUtc),
            _ => update
                .Set(x => x.LifecycleStatus, decision.Kind switch
                {
                    ProductAbbreviationDecisionKind.Approve => ProductAbbreviationLifecycleStatus.ACTIVE,
                    ProductAbbreviationDecisionKind.Reject => ProductAbbreviationLifecycleStatus.REJECTED,
                    _ => ProductAbbreviationLifecycleStatus.CANCELLED
                })
                .Set(x => x.LastDecisionByCanonicalSubjectId, decision.ActorSubjectId)
                .Set(x => x.LastDecisionAtUtc, decision.DecidedAtUtc)
        };
    }

    /// <summary>The entry as it will read after <paramref name="decision"/> — the same fields DecisionUpdate writes.</summary>
    private static ProductAbbreviationRegisterEntry Apply(
        ProductAbbreviationRegisterEntry entry,
        ProductAbbreviationDecision decision)
    {
        var copy = BsonSerializer.Deserialize<ProductAbbreviationRegisterEntry>(entry.ToBsonDocument());
        copy.LastDecisionIdempotencyKey = decision.IdempotencyKey;
        copy.LastDecisionReason = decision.Reason;
        copy.UpdatedAt = decision.DecidedAtUtc;
        copy.Version = entry.Version + 1;
        switch (decision.Kind)
        {
            case ProductAbbreviationDecisionKind.RequestRetirement:
                copy.RetirementRequestId = decision.IdempotencyKey;
                copy.RetirementRequestedByCanonicalSubjectId = decision.ActorSubjectId;
                copy.RetirementRequestedAtUtc = decision.DecidedAtUtc;
                copy.PendingSinceUtcTicks = decision.DecidedAtUtc.UtcTicks;
                break;
            case ProductAbbreviationDecisionKind.ApproveRetirement:
            case ProductAbbreviationDecisionKind.RejectRetirement:
                copy.LifecycleStatus = decision.Kind == ProductAbbreviationDecisionKind.ApproveRetirement
                    ? ProductAbbreviationLifecycleStatus.RETIRED
                    : ProductAbbreviationLifecycleStatus.ACTIVE;
                copy.RetirementRequestId = null;
                copy.RetirementRequestedByCanonicalSubjectId = null;
                copy.RetirementRequestedAtUtc = null;
                copy.LastDecisionByCanonicalSubjectId = decision.ActorSubjectId;
                copy.LastDecisionAtUtc = decision.DecidedAtUtc;
                break;
            default:
                copy.LifecycleStatus = decision.Kind switch
                {
                    ProductAbbreviationDecisionKind.Approve => ProductAbbreviationLifecycleStatus.ACTIVE,
                    ProductAbbreviationDecisionKind.Reject => ProductAbbreviationLifecycleStatus.REJECTED,
                    _ => ProductAbbreviationLifecycleStatus.CANCELLED
                };
                copy.LastDecisionByCanonicalSubjectId = decision.ActorSubjectId;
                copy.LastDecisionAtUtc = decision.DecidedAtUtc;
                break;
        }

        return copy;
    }

    // ══ FIX2 A2 / item 6 — a new request, its claim, its history row and its audit intent: one transaction ════════

    public async Task<ProductAbbreviationRegisterWriteResult> RequestEntryAsync(
        ProductAbbreviationRequestWrite write,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        var entry = write.Entry;
        var allocation = write.Allocation;
        // The entry names its ledger row before anything compares it with a stored request (a replay included).
        entry.AllocationLedgerId = allocation.Id;
        var replay = await GetByAllocationIdempotencyKeyAsync(entry.AllocationIdempotencyKey, cancellationToken);
        if (replay is not null)
        {
            return ReplayOf(replay, entry);
        }

        entry.TenantId = _tenantId;
        entry.IsDeleted = false;
        entry.DeletedAt = null;
        entry.CreatedAt = entry.RequestedAtUtc;
        entry.UpdatedAt = entry.RequestedAtUtc;
        entry.Version = 0;
        entry.LifecycleStatus = ProductAbbreviationLifecycleStatus.REQUESTED;
        entry.PendingSinceUtcTicks = entry.RequestedAtUtc.UtcTicks;
        var evidence = write.BuildEvidence(entry);
        entry.AuditIntents = [evidence.Intent];
        var history = Stored(evidence.History);
        allocation.TenantId = _tenantId;
        allocation.IsDeleted = false;
        allocation.DeletedAt = null;
        allocation.CreatedAt = allocation.AllocatedAtUtc;
        allocation.UpdatedAt = allocation.AllocatedAtUtc;
        allocation.Version = 0;
        ProductAbbreviationRegisterEntry? formerRead = null;

        var outcome = await InTransactionAsync(
            async session =>
            {
                // FIX3 item 3: the code is taken in the ledger FIRST, inside this transaction — a request that is refused
                // or loses a race afterwards (claim, product slot, entry) takes nothing: the abort takes the row back. A ledger row already written under this key (by a request before FIX3
                // whose register write never landed) is reused only when it is the same request.
                var held = await _ledger.Find(session, LedgerByKey(allocation.IdempotencyKey))
                    .FirstOrDefaultAsync(cancellationToken);
                if (held is not null)
                {
                    if (!ProductAbbreviationAllocationLedgerRepository.Matches(held, allocation))
                    {
                        return new(false, null, "ABBREVIATION_IDEMPOTENCY_CONFLICT");
                    }
                }
                else
                {
                    if (await _ledger.Find(session, LedgerByAbbreviation(allocation.NormalizedAbbreviation))
                            .Limit(1).AnyAsync(cancellationToken))
                    {
                        return new(false, null, "ABBREVIATION_ALREADY_ALLOCATED");
                    }

                    await _ledger.InsertOneAsync(session, allocation, cancellationToken: cancellationToken);
                }

                if (entry.ReplacesEntryId is { } formerId)
                {
                    var former = await _collection.Find(session, ById(formerId)).FirstOrDefaultAsync(cancellationToken);
                    formerRead = former;
                    if (former is null || former.GlobalProductId != entry.GlobalProductId)
                    {
                        return new(false, former, "ABBREVIATION_CORRECTION_CONFLICT");
                    }

                    if (former.LifecycleStatus != ProductAbbreviationLifecycleStatus.ACTIVE)
                    {
                        return new(false, former, "ABBREVIATION_CORRECTION_REQUIRES_ACTIVE");
                    }

                    if (former.RetirementRequestId is not null)
                    {
                        return new(false, former, "ABBREVIATION_RETIREMENT_PENDING");
                    }

                    if (former.PendingCorrectionEntryId is { } claimed && claimed != entry.Id
                        && IsPendingCorrectionOf(
                            await _collection.Find(session, ById(claimed)).FirstOrDefaultAsync(cancellationToken),
                            former.Id))
                    {
                        return new(false, former, "ABBREVIATION_CORRECTION_PENDING");
                    }

                    // The claim and the correction it names are written together: there is no moment in which the
                    // claim points at an entry that does not exist yet (FIX2 A2). FIX3 item 13: and only while the code
                    // is still the version the correction was asked against.
                    var claim = ById(former.Id)
                                & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                                    x => x.LifecycleStatus,
                                    ProductAbbreviationLifecycleStatus.ACTIVE)
                                & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.RetirementRequestId, null)
                                & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                                    x => x.PendingCorrectionEntryId,
                                    former.PendingCorrectionEntryId);
                    if (write.ExpectedFormerVersion is { } expectedFormer)
                    {
                        claim &= Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Version, expectedFormer);
                    }

                    var marked = await _collection.UpdateOneAsync(
                        session,
                        claim,
                        Builders<ProductAbbreviationRegisterEntry>.Update
                            .Set(x => x.PendingCorrectionEntryId, entry.Id)
                            .Set(x => x.UpdatedAt, entry.RequestedAtUtc),
                        cancellationToken: cancellationToken);
                    if (marked.ModifiedCount != 1)
                    {
                        return new(false, former, Reclassify);
                    }
                }
                else
                {
                    var open = await _collection.Find(
                            session,
                            OpenEntryForProduct(entry.GlobalProductId, entry.Id))
                        .Limit(1)
                        .AnyAsync(cancellationToken);
                    if (open)
                    {
                        return new(false, null, "ABBREVIATION_PRODUCT_ALREADY_HAS_ENTRY");
                    }

                    // The product's slot: two requests for one product both write it, so the second one's
                    // transaction conflicts instead of both passing the check above (item 6).
                    await _productSlots.UpdateOneAsync(
                        session,
                        new BsonDocument("_id", ProductSlotId(entry.GlobalProductId)),
                        new BsonDocument("$set", new BsonDocument
                        {
                            ["TenantId"] = new BsonBinaryData(_tenantId, GuidRepresentation.Standard),
                            ["GlobalProductId"] = new BsonBinaryData(entry.GlobalProductId, GuidRepresentation.Standard),
                            ["LastRequestedEntryId"] = new BsonBinaryData(entry.Id, GuidRepresentation.Standard),
                            ["UpdatedAtUtcTicks"] = entry.RequestedAtUtc.UtcTicks
                        }),
                        new UpdateOptions { IsUpsert = true },
                        cancellationToken);
                }

                await _collection.InsertOneAsync(session, entry, cancellationToken: cancellationToken);
                await _history.InsertOneAsync(session, history, cancellationToken: cancellationToken);
                return null;
            },
            async () =>
            {
                var again = await GetByAllocationIdempotencyKeyAsync(entry.AllocationIdempotencyKey, cancellationToken);
                if (again is not null)
                {
                    return ReplayOf(again, entry);
                }

                if (entry.ReplacesEntryId is { } formerId
                    && await GetByIdAsync(formerId, cancellationToken) is { } former)
                {
                    if (former.PendingCorrectionEntryId is { } pending && pending != entry.Id
                        && IsPendingCorrectionOf(await GetByIdAsync(pending, cancellationToken), formerId))
                    {
                        return new(false, former, "ABBREVIATION_CORRECTION_PENDING");
                    }

                    if (former.RetirementRequestId is not null)
                    {
                        return new(false, former, "ABBREVIATION_RETIREMENT_PENDING");
                    }

                    if (former.LifecycleStatus != ProductAbbreviationLifecycleStatus.ACTIVE
                        || write.ExpectedFormerVersion is { } expected && former.Version != expected
                        || formerRead is not null
                           && (former.Version != formerRead.Version
                               || former.PendingCorrectionEntryId != formerRead.PendingCorrectionEntryId))
                    {
                        return new(false, former, "CONCURRENCY_CONFLICT");
                    }
                }

                if (entry.ReplacesEntryId is null
                    && await _collection.Find(OpenEntryForProduct(entry.GlobalProductId, entry.Id)).AnyAsync(cancellationToken))
                {
                    return new(false, null, "ABBREVIATION_PRODUCT_ALREADY_HAS_ENTRY");
                }

                if (await _ledger.Find(LedgerByAbbreviation(allocation.NormalizedAbbreviation)
                                       & Builders<ProductAbbreviationAllocationLedger>.Filter.Ne(
                                           x => x.IdempotencyKey, allocation.IdempotencyKey))
                        .Limit(1).AnyAsync(cancellationToken))
                {
                    return new(false, null, "ABBREVIATION_ALREADY_ALLOCATED");
                }

                return null;
            },
            "CONCURRENCY_CONFLICT",
            cancellationToken);
        return outcome ?? new(true, await ReadBackAsync(entry.Id, entry, cancellationToken));
    }

    private FilterDefinition<ProductAbbreviationAllocationLedger> LedgerByKey(string idempotencyKey)
        => Builders<ProductAbbreviationAllocationLedger>.Filter.Eq(x => x.TenantId, _tenantId)
           & Builders<ProductAbbreviationAllocationLedger>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey);

    private FilterDefinition<ProductAbbreviationAllocationLedger> LedgerByAbbreviation(string normalizedAbbreviation)
        => Builders<ProductAbbreviationAllocationLedger>.Filter.Eq(x => x.TenantId, _tenantId)
           & Builders<ProductAbbreviationAllocationLedger>.Filter.Eq(x => x.NormalizedAbbreviation, normalizedAbbreviation);

    /// <summary>
    /// A claim counts only while the entry it names is a REQUESTED correction OF THIS code (FIX2 A2): an entry that was
    /// decided, never inserted, or that replaces something else does not block anything.
    /// </summary>
    private static bool IsPendingCorrectionOf(ProductAbbreviationRegisterEntry? claimed, Guid formerId)
        => claimed is { LifecycleStatus: ProductAbbreviationLifecycleStatus.REQUESTED }
           && claimed.ReplacesEntryId == formerId;

    private FilterDefinition<ProductAbbreviationRegisterEntry> OpenEntryForProduct(Guid globalProductId, Guid exceptEntryId)
        => TenantFilter
           & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.GlobalProductId, globalProductId)
           & Builders<ProductAbbreviationRegisterEntry>.Filter.Ne(x => x.Id, exceptEntryId)
           & Builders<ProductAbbreviationRegisterEntry>.Filter.In(
               x => x.LifecycleStatus,
               [ProductAbbreviationLifecycleStatus.ACTIVE, ProductAbbreviationLifecycleStatus.REQUESTED]);

    private string ProductSlotId(Guid globalProductId) => $"{_tenantId:N}|{globalProductId:N}";

    private ProductAbbreviationHistoryEntry Stored(ProductAbbreviationHistoryEntry history)
    {
        history.TenantId = _tenantId;
        history.IsDeleted = false;
        history.CreatedAt = history.OccurredAtUtc;
        history.UpdatedAt = history.OccurredAtUtc;
        history.Version = 0;
        return history;
    }

    /// <summary>
    /// FIX3 item 5 — the ONE transaction every ABB write goes through. A non-null answer from <paramref name="body"/>
    /// aborts with that answer (the internal "reclassify" marker asks the stored state instead). When the transaction
    /// fails, the stored state decides, never the error label alone: <paramref name="afterAbort"/> re-reads it — if it
    /// moved, that is the answer (mine / another person's / 409); if it did not, a write conflict or a duplicate key is
    /// <paramref name="unchangedConflictCode"/> (409) and anything else — a network or primary failure even when the
    /// driver labels it transient — is ABBREVIATION_PERSISTENCE_UNAVAILABLE (503). Opening the session is inside the
    /// same guard. Null = committed.
    /// </summary>
    private async Task<ProductAbbreviationRegisterWriteResult?> InTransactionAsync(
        Func<IClientSessionHandle, Task<ProductAbbreviationRegisterWriteResult?>> body,
        Func<Task<ProductAbbreviationRegisterWriteResult?>> afterAbort,
        string unchangedConflictCode,
        CancellationToken cancellationToken)
    {
        IClientSessionHandle? session = null;
        try
        {
            session = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
            session.StartTransaction(new TransactionOptions(
                ReadConcern.Snapshot,
                ReadPreference.Primary,
                WriteConcern.WMajority));
            var refused = await body(session);
            if (refused is not null)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return refused.ErrorCode == Reclassify
                    ? await afterAbort() ?? new(false, refused.Entry, unchangedConflictCode)
                    : refused;
            }

            await session.CommitTransactionAsync(cancellationToken);
            return null;
        }
        catch (OperationCanceledException)
        {
            await AbortQuietlyAsync(session);
            throw;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            await AbortQuietlyAsync(session);
            if (TransactionsAreUnsupported(exception))
            {
                return new(false, null, TransactionsUnavailable);
            }

            if (exception is MongoException mongo && IsActiveProductViolation(mongo))
            {
                return new(false, null, "ACTIVE_ABBREVIATION_CONFLICT");
            }

            ProductAbbreviationRegisterWriteResult? after;
            try
            {
                after = await afterAbort();
            }
            catch (Exception readFailure) when (IsPersistenceFailure(readFailure))
            {
                return new(false, null, PersistenceUnavailable);
            }

            if (after is not null)
            {
                return after;
            }

            return exception is MongoException conflict && (IsDuplicateKey(conflict) || IsWriteConflict(conflict))
                ? new(false, null, unchangedConflictCode)
                : new(false, null, PersistenceUnavailable);
        }
        finally
        {
            session?.Dispose();
        }
    }

    /// <summary>
    /// FIX4 item 7 — the entry as stored after a COMMITTED write. The write has happened: a failing read-back must not
    /// turn it into a 500 the caller would retry; the entry as written is answered instead, and the failure is logged.
    /// </summary>
    private async Task<ProductAbbreviationRegisterEntry> ReadBackAsync(
        Guid id,
        ProductAbbreviationRegisterEntry written,
        CancellationToken cancellationToken)
    {
        try
        {
            return await GetByIdAsync(id, cancellationToken) ?? written;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            _logger.LogWarning(
                "mdm.product_abbreviation.read_back_failed EntryId={EntryId} ExceptionType={ExceptionType}",
                id,
                exception.GetType().Name);
            return written;
        }
    }

    private static async Task AbortQuietlyAsync(IClientSessionHandle? session)
    {
        if (session is not { IsInTransaction: true })
        {
            return;
        }

        try
        {
            await session.AbortTransactionAsync(CancellationToken.None);
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            // The server ends an abandoned transaction itself; the answer is decided by the stored state.
        }
    }

    /// <summary>A failure of the database or of the path to it — not a bug in this code.</summary>
    private static bool IsPersistenceFailure(Exception exception)
        => exception is MongoException or TimeoutException || IsStandaloneRefusal(exception);

    /// <summary>
    /// FIX4 item 7 — the driver's own refusal to start a transaction on a standalone server; any other
    /// NotSupportedException is a bug in this code and is not dressed up as an outage.
    /// </summary>
    private static bool IsStandaloneRefusal(Exception exception)
        => exception is NotSupportedException
           && exception.Message.Contains("do not support transactions", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// FIX3 item 6 — a standalone mongod: the driver refuses to start the transaction (NotSupportedException), or, when
    /// it could not tell yet, the server refuses the first statement (IllegalOperation, code 20).
    /// </summary>
    public static bool TransactionsAreUnsupported(Exception exception)
        => IsStandaloneRefusal(exception)
           || exception is MongoCommandException { Code: 20 } command
              && (command.ErrorMessage ?? command.Message).Contains("Transaction numbers", StringComparison.OrdinalIgnoreCase);

    /// <summary>Another transaction wrote the same document first (WriteConflict, 112).</summary>
    private static bool IsWriteConflict(MongoException exception)
        => exception is MongoCommandException { Code: 112 } or MongoWriteException { WriteError.Code: 112 };

    private static bool IsDuplicateKey(MongoException exception) => exception switch
    {
        MongoWriteException write => write.WriteError?.Category == ServerErrorCategory.DuplicateKey,
        MongoBulkWriteException bulk => bulk.WriteErrors.Any(error => error.Category == ServerErrorCategory.DuplicateKey),
        MongoCommandException command => command.Code == 11000,
        _ => false
    };

    /// <summary>
    /// FIX1 S4 — the same request sent again is a replay only while it is still open. A request that was rejected,
    /// cancelled, approved or retired is CLOSED: sending it again is not a new request and not a success either.
    /// </summary>
    private static ProductAbbreviationRegisterWriteResult ReplayOf(
        ProductAbbreviationRegisterEntry persisted,
        ProductAbbreviationRegisterEntry requested)
    {
        // FIX3 item 11: a request is repeated only by the person who made it. Anyone else sending the same key is
        // refused — never told "your request is pending", never sent into an evidence write that can never match.
        if (!string.Equals(
                persisted.RequestedByCanonicalSubjectId,
                requested.RequestedByCanonicalSubjectId,
                StringComparison.Ordinal))
        {
            return new(false, persisted, "ABBREVIATION_IDEMPOTENCY_KEY_REUSED");
        }

        if (!SameRequest(persisted, requested))
        {
            return new(false, persisted, "ABBREVIATION_IDEMPOTENCY_CONFLICT");
        }

        return persisted.LifecycleStatus == ProductAbbreviationLifecycleStatus.REQUESTED
            ? new(true, persisted, IsReplay: true)
            : new(false, persisted, "ABBREVIATION_REQUEST_ALREADY_CLOSED");
    }

    private static bool SameRequest(
        ProductAbbreviationRegisterEntry persisted,
        ProductAbbreviationRegisterEntry requested)
        => persisted.Id == requested.Id
           && persisted.GlobalProductId == requested.GlobalProductId
           && persisted.NormalizedAbbreviation == requested.NormalizedAbbreviation
           && persisted.AllocationLedgerId == requested.AllocationLedgerId
           && persisted.ReplacesEntryId == requested.ReplacesEntryId;

    private void EnsureIndexes()
    {
        DropRetiredIndex();
        var activeFilter = Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.IsDeleted, false)
                           & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                               x => x.LifecycleStatus,
                               ProductAbbreviationLifecycleStatus.ACTIVE);
        _collection.Indexes.CreateMany(
        [
            new CreateIndexModel<ProductAbbreviationRegisterEntry>(
                Builders<ProductAbbreviationRegisterEntry>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.GlobalProductId),
                new CreateIndexOptions<ProductAbbreviationRegisterEntry>
                {
                    Unique = true,
                    Name = "ux_mdm_product_abbreviation_register_tenant_active_product",
                    PartialFilterExpression = activeFilter
                }),
            new CreateIndexModel<ProductAbbreviationRegisterEntry>(
                Builders<ProductAbbreviationRegisterEntry>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.AllocationIdempotencyKey),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "ux_mdm_product_abbreviation_register_tenant_allocation_command"
                }),
            new CreateIndexModel<ProductAbbreviationRegisterEntry>(
                Builders<ProductAbbreviationRegisterEntry>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.NormalizedAbbreviation)
                    .Ascending(x => x.LifecycleStatus),
                new CreateIndexOptions { Name = "ix_mdm_product_abbreviation_register_tenant_resolution" }),
            // FIX2 A3 / FIX3 item 10: the Task Center reads pending work in the order it started waiting — from two
            // partial indexes that hold ONLY waiting rows, so a page never walks the tenant's decided ones.
            new CreateIndexModel<ProductAbbreviationRegisterEntry>(
                Builders<ProductAbbreviationRegisterEntry>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PendingSinceUtcTicks)
                    .Ascending(x => x.Id)
                    // A trailing field only to keep the key pattern distinct from the FIX2 index left on existing
                    // databases (two indexes on one key pattern differing only by partiality need a newer server).
                    .Ascending(x => x.LifecycleStatus),
                new CreateIndexOptions<ProductAbbreviationRegisterEntry>
                {
                    Name = PendingRequestIndexName,
                    PartialFilterExpression = RequestedWork
                }),
            new CreateIndexModel<ProductAbbreviationRegisterEntry>(
                Builders<ProductAbbreviationRegisterEntry>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PendingSinceUtcTicks)
                    .Ascending(x => x.Id)
                    .Ascending(x => x.RetirementRequestId),
                new CreateIndexOptions<ProductAbbreviationRegisterEntry>
                {
                    Name = PendingRetirementIndexName,
                    PartialFilterExpression = Builders<ProductAbbreviationRegisterEntry>.Filter.Type(
                        x => x.RetirementRequestId,
                        BsonType.String)
                })
        ]);
        // Created up front: the first request of a product upserts its slot inside a transaction.
        _productSlots.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("TenantId").Ascending("GlobalProductId"),
            new CreateIndexOptions { Name = "ix_mdm_product_abbreviation_product_slots_tenant_product" }));
    }

    /// <summary>
    /// FIX4 item 5 — FIX2's pending-order index held every row of every tenant; the two partial indexes replaced it.
    /// Dropped where it still exists, by its exact name only (no other index is touched); idempotent. Once per process
    /// per database (EK 7): marked after the check completed, so a failed check is tried again by the next repository.
    /// </summary>
    private void DropRetiredIndex()
    {
        // EK 7: the repository is built once per request; the index list is read once per process per database.
        var marker = _database.DatabaseNamespace.DatabaseName;
        if (RetiredIndexChecked.ContainsKey(marker))
        {
            return;
        }

        DropRetiredIndexNow();
        RetiredIndexChecked.TryAdd(marker, 0);
    }

    private void DropRetiredIndexNow()
    {
        var present = _collection.Indexes.List().ToList()
            .Any(index => index.TryGetValue("name", out var name) && name == RetiredPendingOrderIndexName);
        if (!present)
        {
            return;
        }

        try
        {
            _collection.Indexes.DropOne(RetiredPendingOrderIndexName);
        }
        catch (MongoCommandException exception) when (exception.Code == 27)
        {
            // IndexNotFound: another instance dropped it first.
        }
    }

    private FilterDefinition<ProductAbbreviationRegisterEntry> TenantFilter
        => Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.TenantId, _tenantId)
           & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.IsDeleted, false);
}
