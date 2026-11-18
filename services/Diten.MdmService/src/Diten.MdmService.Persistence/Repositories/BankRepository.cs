using System.Text.RegularExpressions;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.BankMaster;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

/// <summary>
/// MOD-0289 DL-1 — the bank store (<c>mdm_banks</c>). Every read and every write goes through <see cref="TenantFilter"/>
/// (TenantId + not deleted); the tenant is the trusted one, never a parameter, and an empty tenant is refused. Every write is
/// ONE conditional write of the business fields AND the audit intent (a document write is atomic): there is no state in
/// which the bank changed and its intent did not land (AC-6). The update filter carries the expected <c>Version</c> (AC-9).
/// The base <c>UpdateAsync</c> (no version in its filter) is not used.
/// </summary>
public sealed class BankRepository : IBankRepository
{
    public const string CollectionName = "mdm_banks";
    public const string IndexSet = "bank-unique-v1";
    public const string CodeIndex = "ux_mdm_bank_code";
    public const string BicIndex = "ux_mdm_bank_bic";
    public const string NationalCodeIndex = "ux_mdm_bank_national_code";

    private readonly IMongoClient _client;
    private readonly IMongoCollection<Bank> _banks;
    private readonly Guid _tenantId;
    private readonly BrandProductStoreOptions _storeOptions;
    private readonly ILogger? _logger;
    private readonly string _indexKey;

    public BankRepository(
        IMongoDatabase database,
        ITenantContext tenantContext,
        BrandProductStoreOptions? storeOptions = null,
        ILogger<BankRepository>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(tenantContext);
        if (tenantContext.TenantId == Guid.Empty)
            throw new InvalidOperationException("A trusted tenant is required for bank persistence.");
        _client = database.Client;
        _banks = database.GetCollection<Bank>(CollectionName);
        _tenantId = tenantContext.TenantId;
        // No options = the production answer: a write that cannot be atomic (bulk delete without a transaction) is refused.
        _storeOptions = storeOptions ?? new BrandProductStoreOptions();
        _logger = logger;
        // No Mongo call here (MongoIndexReadiness, BL-598): the unique indexes are prepared once per process; every write
        // waits for them (no write without the unique index that keeps two banks from one BIC), a read only starts them.
        _indexKey = MongoIndexReadiness.KeyOf(_banks, IndexSet);
    }

    /// <summary>The ONE tenant gate of this store: every filter below starts from it (AC-3 sabotage point).</summary>
    private FilterDefinition<Bank> TenantFilter => Builders<Bank>.Filter.And(
        Builders<Bank>.Filter.Eq(x => x.TenantId, _tenantId),
        Builders<Bank>.Filter.Eq(x => x.IsDeleted, false));

    // Tests set a short budget (a closed port must not hold a test for the default 10 s).
    internal TimeSpan WriteIndexBudget { get; init; } = MongoIndexReadiness.DefaultWriteBudget;

    internal Task WhenIndexesReadyAsync(CancellationToken cancellationToken = default) =>
        MongoIndexReadiness.EnsureForWriteAsync(_indexKey, CreateIndexesAsync, WriteIndexBudget, cancellationToken, refresh: true);

    private async Task<bool> IndexesReadyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await MongoIndexReadiness.EnsureForWriteAsync(
                _indexKey, CreateIndexesAsync, WriteIndexBudget, cancellationToken, logger: _logger);
            return true;
        }
        catch (MongoIndexNotReadyException)
        {
            return false;
        }
    }

    private async Task CreateIndexesAsync(CancellationToken cancellationToken)
    {
        await _banks.Indexes.CreateManyAsync(
        [
            // Silinen bankanın kodu da dolu sayılır (RCS-001 §6): NO partial filter.
            new CreateIndexModel<Bank>(
                Builders<Bank>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Code),
                new CreateIndexOptions { Unique = true, Name = CodeIndex }),
            new CreateIndexModel<Bank>(
                Builders<Bank>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Bic),
                new CreateIndexOptions<Bank>
                {
                    Unique = true,
                    Name = BicIndex,
                    PartialFilterExpression = new BsonDocumentFilterDefinition<Bank>(new BsonDocument
                    {
                        { nameof(Bank.Bic), new BsonDocument("$type", "string") },
                        { nameof(Bank.IsDeleted), false }
                    })
                }),
            new CreateIndexModel<Bank>(
                Builders<Bank>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CountryCode).Ascending(x => x.NationalBankCode),
                new CreateIndexOptions<Bank>
                {
                    Unique = true,
                    Name = NationalCodeIndex,
                    PartialFilterExpression = new BsonDocumentFilterDefinition<Bank>(new BsonDocument
                    {
                        { nameof(Bank.NationalBankCode), new BsonDocument("$type", "string") },
                        { nameof(Bank.IsDeleted), false }
                    })
                }),
            // The register's own default order (and the tenant filter's support).
            new CreateIndexModel<Bank>(
                Builders<Bank>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.IsDeleted).Ascending(x => x.Code),
                new CreateIndexOptions { Name = "ix_mdm_bank_tenant_code" })
        ], cancellationToken);
    }

    public async Task<Bank?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        MongoIndexReadiness.Start(_indexKey, CreateIndexesAsync, _logger);
        return await _banks.Find(TenantFilter & Builders<Bank>.Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Bank>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0) return [];
        MongoIndexReadiness.Start(_indexKey, CreateIndexesAsync, _logger);
        return await _banks.Find(TenantFilter & Builders<Bank>.Filter.In(x => x.Id, ids)).ToListAsync(cancellationToken);
    }

    public async Task<BankPage> ListAsync(BankListCriteria criteria, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        MongoIndexReadiness.Start(_indexKey, CreateIndexesAsync, _logger);
        var filter = TenantFilter;
        if (criteria.Status is { } status) filter &= Builders<Bank>.Filter.Eq(x => x.Status, status);
        if (criteria.CountryCode is { } country) filter &= Builders<Bank>.Filter.Eq(x => x.CountryCode, country);
        if (!string.IsNullOrEmpty(criteria.Search))
        {
            // AC-59 (KARAR D51/4): the text is DATA, never a pattern. Regex.Escape makes every metacharacter literal and the
            // "i" option makes the match case-insensitive; the search never leaves the tenant filter above.
            var pattern = new BsonRegularExpression(Regex.Escape(criteria.Search), "i");
            filter &= Builders<Bank>.Filter.Or(
                Builders<Bank>.Filter.Regex(x => x.Name, pattern),
                Builders<Bank>.Filter.Regex(x => x.Code, pattern),
                Builders<Bank>.Filter.Regex(x => x.Bic, pattern));
        }

        SortDefinition<Bank> sort = criteria.Order switch
        {
            BankListOrder.Name => Sort(x => x.Name, criteria.Descending),
            BankListOrder.CountryCode => Sort(x => x.CountryCode, criteria.Descending),
            BankListOrder.Status => Sort(x => x.Status, criteria.Descending),
            BankListOrder.CreatedAt => Sort(x => x.CreatedAt, criteria.Descending),
            _ => Sort(x => x.Code, criteria.Descending)
        };
        // A stable page: the id breaks ties of the chosen column.
        sort = Builders<Bank>.Sort.Combine(sort, Builders<Bank>.Sort.Ascending(x => x.Id));

        var total = await _banks.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _banks.Find(filter).Sort(sort)
            .Skip((criteria.Page - 1) * criteria.PageSize).Limit(criteria.PageSize).ToListAsync(cancellationToken);
        return new BankPage(items, total);
    }

    private static SortDefinition<Bank> Sort(System.Linq.Expressions.Expression<Func<Bank, object>> field, bool descending) =>
        descending ? Builders<Bank>.Sort.Descending(field) : Builders<Bank>.Sort.Ascending(field);

    public async Task<BankStoreOutcome> CreateAsync(Bank bank, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bank);
        // The depo refuses an empty tenant and a document of another tenant (the tenant is never the caller's choice).
        if (bank.TenantId == Guid.Empty || bank.TenantId != _tenantId)
            throw new InvalidOperationException("A bank is written for the trusted tenant only.");
        if (bank.AuditIntents.Count != 1
            || !BankAuditIntentFactory.IsWellFormed(bank.AuditIntents[0], AuditAggregateType.Bank, bank.TenantId, bank.Id))
            return BankStoreOutcome.AuditIntentUnavailable;
        if (!await IndexesReadyAsync(cancellationToken)) return BankStoreOutcome.IndexesNotReady;

        try
        {
            await _banks.InsertOneAsync(bank, cancellationToken: cancellationToken);
            return BankStoreOutcome.Written;
        }
        catch (Exception exception) when (BankStoreSupport.TryClassifyDuplicate(exception, out var outcome))
        {
            return outcome;
        }
        catch (Exception exception) when (BankStoreSupport.IsStoreFailure(exception, cancellationToken))
        {
            _logger?.LogWarning(exception, "mdm.bank.create_failed: nothing was written.");
            return BankStoreOutcome.AuditIntentUnavailable;
        }
    }

    public async Task<BankStoreOutcome> UpdateAsync(
        Bank bank, int expectedVersion, LocalAuditIntent intent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bank);
        if (!BankAuditIntentFactory.IsWellFormed(intent, AuditAggregateType.Bank, _tenantId, bank.Id))
            return BankStoreOutcome.AuditIntentUnavailable;
        if (!await IndexesReadyAsync(cancellationToken)) return BankStoreOutcome.IndexesNotReady;

        var update = Builders<Bank>.Update
            .Set(x => x.Name, bank.Name)
            .Set(x => x.CountryCode, bank.CountryCode)
            .Set(x => x.Bic, bank.Bic)
            .Set(x => x.NationalBankCode, bank.NationalBankCode)
            .Set(x => x.DomesticKeyIncludesBranch, bank.DomesticKeyIncludesBranch)
            .Set(x => x.DomesticKeyFenceVersion, bank.DomesticKeyFenceVersion)
            .Set(x => x.Status, bank.Status)
            .Set(x => x.ActivatedBy, bank.ActivatedBy)
            .Set(x => x.ActivatedAt, bank.ActivatedAt)
            .Set(x => x.DeactivatedBy, bank.DeactivatedBy)
            .Set(x => x.DeactivatedAt, bank.DeactivatedAt)
            .Set(x => x.DeactivationReasonCode, bank.DeactivationReasonCode)
            .Set(x => x.UpdatedBy, bank.UpdatedBy)
            .Set(x => x.UpdatedAt, bank.UpdatedAt)
            .Inc(x => x.Version, 1)
            .Push(x => x.AuditIntents, intent);
        return await ConditionalAsync(bank.Id, expectedVersion, update, onlyNeverActivatedDraft: false, cancellationToken);
    }

    public async Task<BankStoreOutcome> SoftDeleteAsync(
        Bank bank, int expectedVersion, LocalAuditIntent intent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bank);
        if (!BankAuditIntentFactory.IsWellFormed(intent, AuditAggregateType.Bank, _tenantId, bank.Id))
            return BankStoreOutcome.AuditIntentUnavailable;
        if (!await IndexesReadyAsync(cancellationToken)) return BankStoreOutcome.IndexesNotReady;
        return await ConditionalAsync(
            bank.Id, expectedVersion, SoftDeleteUpdate(bank, intent), onlyNeverActivatedDraft: true, cancellationToken);
    }

    public async Task<BankStoreOutcome> SoftDeleteManyAsync(
        IReadOnlyList<BankSoftDelete> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0) return BankStoreOutcome.Written;
        if (items.Any(x => !BankAuditIntentFactory.IsWellFormed(x.Intent, AuditAggregateType.Bank, _tenantId, x.Bank.Id)))
            return BankStoreOutcome.AuditIntentUnavailable;
        if (!await IndexesReadyAsync(cancellationToken)) return BankStoreOutcome.IndexesNotReady;

        // All or none: one transaction (the shared master data policy: majority writes, transient errors re-run, no
        // transaction support outside Development = refused). One document that no longer matches aborts everything.
        try
        {
            var outcome = await MasterDataAtomicWrite.RunAsync(_client, _storeOptions, _logger, async session =>
            {
                foreach (var item in items)
                {
                    var filter = ConditionFilter(item.Bank.Id, item.ExpectedVersion, onlyNeverActivatedDraft: true);
                    var update = SoftDeleteUpdate(item.Bank, item.Intent);
                    var result = session is null
                        ? await _banks.UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
                        : await _banks.UpdateOneAsync(session, filter, update, cancellationToken: cancellationToken);
                    if (result.MatchedCount != 1) return BrandWriteOutcome.Stale;
                }

                return BrandWriteOutcome.Written;
            }, cancellationToken);
            return outcome switch
            {
                BrandWriteOutcome.Written => BankStoreOutcome.Written,
                BrandWriteOutcome.Stale => BankStoreOutcome.Stale,
                // The write could not be made atomic (or its commit stayed unknown): refused, the caller repeats it.
                _ => BankStoreOutcome.AuditIntentUnavailable
            };
        }
        catch (Exception exception) when (BankStoreSupport.IsStoreFailure(exception, cancellationToken))
        {
            _logger?.LogWarning(exception, "mdm.bank.bulk_delete_failed: nothing was written.");
            return BankStoreOutcome.AuditIntentUnavailable;
        }
    }

    private static UpdateDefinition<Bank> SoftDeleteUpdate(Bank bank, LocalAuditIntent intent)
    {
        var now = DateTimeOffset.UtcNow;
        return Builders<Bank>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, now)
            .Set(x => x.UpdatedAt, now)
            .Set(x => x.UpdatedBy, bank.UpdatedBy)
            .Inc(x => x.Version, 1)
            .Push(x => x.AuditIntents, intent);
    }

    private FilterDefinition<Bank> ConditionFilter(Guid id, int expectedVersion, bool onlyNeverActivatedDraft)
    {
        // The version in the filter is the optimistic lock (AC-9): without it a stale write would overwrite silently.
        var filter = TenantFilter
                     & Builders<Bank>.Filter.Eq(x => x.Id, id)
                     & Builders<Bank>.Filter.Eq(x => x.Version, expectedVersion);
        if (onlyNeverActivatedDraft)
            filter &= Builders<Bank>.Filter.Eq(x => x.Status, BankLifecycleStatus.Draft)
                      & Builders<Bank>.Filter.Eq(x => x.ActivatedAt, null);
        return filter;
    }

    private async Task<BankStoreOutcome> ConditionalAsync(
        Guid id, int expectedVersion, UpdateDefinition<Bank> update, bool onlyNeverActivatedDraft,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _banks.UpdateOneAsync(
                ConditionFilter(id, expectedVersion, onlyNeverActivatedDraft), update, cancellationToken: cancellationToken);
            return result.MatchedCount == 1 ? BankStoreOutcome.Written : BankStoreOutcome.Stale;
        }
        catch (Exception exception) when (BankStoreSupport.TryClassifyDuplicate(exception, out var outcome))
        {
            return outcome;
        }
        catch (Exception exception) when (BankStoreSupport.IsStoreFailure(exception, cancellationToken))
        {
            _logger?.LogWarning(exception, "mdm.bank.write_failed: nothing was written.");
            return BankStoreOutcome.AuditIntentUnavailable;
        }
    }
}

/// <summary>What the bank and branch stores share: which unique index a duplicate key came from, and what is a store failure.</summary>
internal static class BankStoreSupport
{
    public static bool TryClassifyDuplicate(Exception exception, out BankStoreOutcome outcome)
    {
        outcome = default;
        if (!MasterDataAtomicWrite.IsDuplicateKey(exception)) return false;
        var text = exception.ToString();
        if (text.Contains(BankRepository.BicIndex, StringComparison.Ordinal)) outcome = BankStoreOutcome.BicConflict;
        else if (text.Contains(BankRepository.NationalCodeIndex, StringComparison.Ordinal)) outcome = BankStoreOutcome.NationalCodeConflict;
        else if (text.Contains(BankRepository.CodeIndex, StringComparison.Ordinal)) outcome = BankStoreOutcome.CodeConflict;
        else if (text.Contains(BankBranchRepository.CodeIndex, StringComparison.Ordinal)) outcome = BankStoreOutcome.BranchCodeConflict;
        else return false;
        return true;
    }

    /// <summary>A Mongo or timeout failure of the write itself (not the caller's cancellation): nothing was written.</summary>
    public static bool IsStoreFailure(Exception exception, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested
        && exception is MongoException or TimeoutException or InvalidOperationException { InnerException: MongoException };
}
