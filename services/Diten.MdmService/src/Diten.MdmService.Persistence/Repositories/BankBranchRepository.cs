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
/// MOD-0289 DL-1 — the branch store (<c>mdm_bank_branches</c>); the same rules as <see cref="BankRepository"/>: one tenant gate,
/// one conditional write of fields and intent, the version in every update filter, the unique branch code per bank.
/// </summary>
public sealed class BankBranchRepository : IBankBranchRepository
{
    public const string CollectionName = "mdm_bank_branches";
    public const string IndexSet = "bank-branch-unique-v1";
    public const string CodeIndex = "ux_mdm_bank_branch_code";

    private readonly IMongoCollection<BankBranch> _branches;
    private readonly Guid _tenantId;
    private readonly ILogger? _logger;
    private readonly string _indexKey;

    public BankBranchRepository(
        IMongoDatabase database, ITenantContext tenantContext, ILogger<BankBranchRepository>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(tenantContext);
        if (tenantContext.TenantId == Guid.Empty)
            throw new InvalidOperationException("A trusted tenant is required for bank branch persistence.");
        _branches = database.GetCollection<BankBranch>(CollectionName);
        _tenantId = tenantContext.TenantId;
        _logger = logger;
        _indexKey = MongoIndexReadiness.KeyOf(_branches, IndexSet);
    }

    /// <summary>The ONE tenant gate of this store (AC-3 sabotage point).</summary>
    private FilterDefinition<BankBranch> TenantFilter => Builders<BankBranch>.Filter.And(
        Builders<BankBranch>.Filter.Eq(x => x.TenantId, _tenantId),
        Builders<BankBranch>.Filter.Eq(x => x.IsDeleted, false));

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
        await _branches.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BankBranch>(
                Builders<BankBranch>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.BankId).Ascending(x => x.BranchCode),
                new CreateIndexOptions<BankBranch>
                {
                    Unique = true,
                    Name = CodeIndex,
                    PartialFilterExpression = new BsonDocumentFilterDefinition<BankBranch>(
                        new BsonDocument { { nameof(BankBranch.IsDeleted), false } })
                })
        ], cancellationToken);
    }

    public async Task<BankBranch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        MongoIndexReadiness.Start(_indexKey, CreateIndexesAsync, _logger);
        return await _branches.Find(TenantFilter & Builders<BankBranch>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<BankBranchPage> ListAsync(BankBranchListCriteria criteria, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        MongoIndexReadiness.Start(_indexKey, CreateIndexesAsync, _logger);
        var filter = TenantFilter & Builders<BankBranch>.Filter.Eq(x => x.BankId, criteria.BankId);
        if (criteria.Status is { } status) filter &= Builders<BankBranch>.Filter.Eq(x => x.Status, status);
        if (!string.IsNullOrEmpty(criteria.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(criteria.Search), "i");
            filter &= Builders<BankBranch>.Filter.Or(
                Builders<BankBranch>.Filter.Regex(x => x.Name, pattern),
                Builders<BankBranch>.Filter.Regex(x => x.BranchCode, pattern),
                Builders<BankBranch>.Filter.Regex(x => x.City, pattern));
        }

        var total = await _branches.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _branches.Find(filter)
            .Sort(Builders<BankBranch>.Sort.Ascending(x => x.BranchCode).Ascending(x => x.Id))
            .Skip((criteria.Page - 1) * criteria.PageSize).Limit(criteria.PageSize).ToListAsync(cancellationToken);
        return new BankBranchPage(items, total);
    }

    public async Task<BankStoreOutcome> CreateAsync(BankBranch branch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(branch);
        if (branch.TenantId == Guid.Empty || branch.TenantId != _tenantId)
            throw new InvalidOperationException("A bank branch is written for the trusted tenant only.");
        if (branch.AuditIntents.Count != 1
            || !BankAuditIntentFactory.IsWellFormed(branch.AuditIntents[0], AuditAggregateType.BankBranch, branch.TenantId, branch.Id))
            return BankStoreOutcome.AuditIntentUnavailable;
        if (!await IndexesReadyAsync(cancellationToken)) return BankStoreOutcome.IndexesNotReady;

        try
        {
            await _branches.InsertOneAsync(branch, cancellationToken: cancellationToken);
            return BankStoreOutcome.Written;
        }
        catch (Exception exception) when (BankStoreSupport.TryClassifyDuplicate(exception, out var outcome))
        {
            return outcome;
        }
        catch (Exception exception) when (BankStoreSupport.IsStoreFailure(exception, cancellationToken))
        {
            _logger?.LogWarning(exception, "mdm.bank_branch.create_failed: nothing was written.");
            return BankStoreOutcome.AuditIntentUnavailable;
        }
    }

    public async Task<BankStoreOutcome> UpdateAsync(
        BankBranch branch, int expectedVersion, LocalAuditIntent intent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(branch);
        if (!BankAuditIntentFactory.IsWellFormed(intent, AuditAggregateType.BankBranch, _tenantId, branch.Id))
            return BankStoreOutcome.AuditIntentUnavailable;
        if (!await IndexesReadyAsync(cancellationToken)) return BankStoreOutcome.IndexesNotReady;

        var update = Builders<BankBranch>.Update
            .Set(x => x.BranchCode, branch.BranchCode)
            .Set(x => x.Name, branch.Name)
            .Set(x => x.CountryCode, branch.CountryCode)
            .Set(x => x.City, branch.City)
            .Set(x => x.Bic, branch.Bic)
            .Set(x => x.DomesticKeyFenceVersion, branch.DomesticKeyFenceVersion)
            .Set(x => x.Status, branch.Status)
            .Set(x => x.ActivatedBy, branch.ActivatedBy)
            .Set(x => x.ActivatedAt, branch.ActivatedAt)
            .Set(x => x.DeactivatedBy, branch.DeactivatedBy)
            .Set(x => x.DeactivatedAt, branch.DeactivatedAt)
            .Set(x => x.DeactivationReasonCode, branch.DeactivationReasonCode)
            .Set(x => x.UpdatedBy, branch.UpdatedBy)
            .Set(x => x.UpdatedAt, branch.UpdatedAt)
            .Inc(x => x.Version, 1)
            .Push(x => x.AuditIntents, intent);
        return await ConditionalAsync(branch.Id, expectedVersion, update, onlyNeverActivatedDraft: false, cancellationToken);
    }

    public async Task<BankStoreOutcome> SoftDeleteAsync(
        BankBranch branch, int expectedVersion, LocalAuditIntent intent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(branch);
        if (!BankAuditIntentFactory.IsWellFormed(intent, AuditAggregateType.BankBranch, _tenantId, branch.Id))
            return BankStoreOutcome.AuditIntentUnavailable;
        if (!await IndexesReadyAsync(cancellationToken)) return BankStoreOutcome.IndexesNotReady;

        var now = DateTimeOffset.UtcNow;
        var update = Builders<BankBranch>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, now)
            .Set(x => x.UpdatedAt, now)
            .Set(x => x.UpdatedBy, branch.UpdatedBy)
            .Inc(x => x.Version, 1)
            .Push(x => x.AuditIntents, intent);
        return await ConditionalAsync(branch.Id, expectedVersion, update, onlyNeverActivatedDraft: true, cancellationToken);
    }

    private async Task<BankStoreOutcome> ConditionalAsync(
        Guid id, int expectedVersion, UpdateDefinition<BankBranch> update, bool onlyNeverActivatedDraft,
        CancellationToken cancellationToken)
    {
        var filter = TenantFilter
                     & Builders<BankBranch>.Filter.Eq(x => x.Id, id)
                     // The version in the filter is the optimistic lock (AC-9).
                     & Builders<BankBranch>.Filter.Eq(x => x.Version, expectedVersion);
        if (onlyNeverActivatedDraft)
            filter &= Builders<BankBranch>.Filter.Eq(x => x.Status, BankLifecycleStatus.Draft)
                      & Builders<BankBranch>.Filter.Eq(x => x.ActivatedAt, null);
        try
        {
            var result = await _branches.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.MatchedCount == 1 ? BankStoreOutcome.Written : BankStoreOutcome.Stale;
        }
        catch (Exception exception) when (BankStoreSupport.TryClassifyDuplicate(exception, out var outcome))
        {
            return outcome;
        }
        catch (Exception exception) when (BankStoreSupport.IsStoreFailure(exception, cancellationToken))
        {
            _logger?.LogWarning(exception, "mdm.bank_branch.write_failed: nothing was written.");
            return BankStoreOutcome.AuditIntentUnavailable;
        }
    }
}
