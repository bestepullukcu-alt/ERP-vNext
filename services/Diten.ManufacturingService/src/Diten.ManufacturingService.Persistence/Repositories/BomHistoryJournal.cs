using Diten.ManufacturingService.Domain.Entities;
using Diten.ManufacturingService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.ManufacturingService.Persistence.Repositories;

/// <summary>
/// AUD-001 yol c — BOM sürümleri ve geçmişleri TEK Mongo işleminde (snapshot / majority) yazılır. Bir yazım başarısız
/// olursa işlem iptal edilir; ne BOM ne geçmiş kalır (K2 fail-closed). Geçmiş koleksiyonuna yalnız ekleme yapılır.
/// Eşzamanlılık: güncellenen her sürüm beklenen <c>Version</c> ile filtrelenir; tutmazsa ya da eşsiz index (item başına
/// tek Effective, revizyon no) ihlal edilirse <see cref="BomCommitResult.Conflict"/>.
/// </summary>
public sealed class BomHistoryJournal(IMongoDatabase database) : IBomHistoryJournal
{
    private const int MaxTransientRetries = 3;

    private readonly IMongoCollection<BomVersion> _boms = database.GetCollection<BomVersion>(BomCollections.Boms);
    private readonly IMongoCollection<BomHistoryEntry> _history = database.GetCollection<BomHistoryEntry>(BomCollections.History);

    public async Task<BomCommitResult> CommitAsync(BomChangeSet changeSet, CancellationToken ct)
    {
        if (changeSet.History.Count == 0)
        {
            throw new InvalidOperationException("A BOM change without a history entry is not allowed (AUD-001 path c).");
        }

        EnsureScoped(changeSet);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var session = await database.Client.StartSessionAsync(cancellationToken: ct);
                session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority));
                try
                {
                    if (changeSet.Inserted is { } inserted)
                    {
                        await _boms.InsertOneAsync(session, inserted, cancellationToken: ct);
                    }

                    foreach (var (entity, expected) in changeSet.Updated)
                    {
                        var filter = BomRepository.Scope(changeSet.TenantId, changeSet.LegalEntityId)
                            & Builders<BomVersion>.Filter.Eq(b => b.Id, entity.Id)
                            & Builders<BomVersion>.Filter.Eq(b => b.Version, expected);
                        var result = await _boms.ReplaceOneAsync(session, filter, entity, cancellationToken: ct);
                        if (result.MatchedCount != 1)
                        {
                            await session.AbortTransactionAsync(CancellationToken.None);
                            return BomCommitResult.Conflict;
                        }
                    }

                    await _history.InsertManyAsync(session, changeSet.History, cancellationToken: ct);
                    await session.CommitTransactionAsync(ct);
                    return BomCommitResult.Committed;
                }
                catch
                {
                    if (session.IsInTransaction)
                    {
                        try { await session.AbortTransactionAsync(CancellationToken.None); } catch (MongoException) { }
                    }

                    throw;
                }
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                return BomCommitResult.Conflict;
            }
            catch (MongoBulkWriteException ex) when (ex.WriteErrors.Any(e => e.Category == ServerErrorCategory.DuplicateKey))
            {
                return BomCommitResult.Conflict;
            }
            catch (MongoException ex) when (ex.HasErrorLabel("TransientTransactionError") && attempt < MaxTransientRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20 * attempt), ct);
            }
            catch (MongoException ex) when (ex.HasErrorLabel("TransientTransactionError"))
            {
                // Concurrent writers kept colliding (e.g. two releases superseding the same version).
                _ = ex;
                return BomCommitResult.Conflict;
            }
            catch (Exception ex) when (ex is MongoException or TimeoutException)
            {
                throw new BomPersistenceUnavailableException("BOM change was not saved; nothing was written.", ex);
            }
        }
    }

    public async Task<IReadOnlyList<BomHistoryEntry>> ListAsync(Guid tenantId, Guid legalEntityId, Guid bomVersionId, CancellationToken ct)
    {
        try
        {
            var f = Builders<BomHistoryEntry>.Filter;
            return await _history
                .Find(f.Eq(h => h.TenantId, tenantId) & f.Eq(h => h.LegalEntityId, legalEntityId) & f.Eq(h => h.BomVersionId, bomVersionId))
                .SortBy(h => h.OccurredAtUtc)
                .ToListAsync(ct);
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            throw new BomPersistenceUnavailableException("BOM history read failed.", ex);
        }
    }

    private static void EnsureScoped(BomChangeSet changeSet)
    {
        bool InScope(BomVersion b) => b.TenantId == changeSet.TenantId && b.LegalEntityId == changeSet.LegalEntityId;
        if ((changeSet.Inserted is { } i && !InScope(i))
            || changeSet.Updated.Any(u => !InScope(u.Entity))
            || changeSet.History.Any(h => h.TenantId != changeSet.TenantId || h.LegalEntityId != changeSet.LegalEntityId))
        {
            throw new InvalidOperationException("Every document in a BOM change must carry the change's tenant and legal entity.");
        }
    }
}
