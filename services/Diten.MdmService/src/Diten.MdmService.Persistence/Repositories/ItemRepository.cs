using System.Text.RegularExpressions;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

// MOD-0290-FU04 S1: mdm_items. Tenant-bounded; "live" = IsDeleted ≠ true (a legacy document without the field is live).
public sealed class ItemRepository : IItemRepository
{
    public const string CollectionName = "mdm_items";
    internal const string NameKeyIndexName = "ux_mdm_items_tenant_name_key";
    private readonly IMongoCollection<Item> _items;
    private readonly IMongoCollection<CodeReservation> _reservations;
    private readonly Guid _tenantId;

    public ItemRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _items = database.GetCollection<Item>(CollectionName);
        _reservations = database.GetCollection<CodeReservation>("mdm_code_reservations");
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    private FilterDefinition<Item> Live =>
        Builders<Item>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<Item>.Filter.Ne(x => x.IsDeleted, true);

    public Task<Item?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _items.Find(Live & Builders<Item>.Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(cancellationToken)!;

    public Task<Item?> GetByCreationCommandIdAsync(string creationCommandId, CancellationToken cancellationToken = default) =>
        _items.Find(Builders<Item>.Filter.Eq(x => x.TenantId, _tenantId)
                    & Builders<Item>.Filter.Eq(x => x.CreationCommandId, creationCommandId))
            .FirstOrDefaultAsync(cancellationToken)!;

    public Task<Item?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default) =>
        _items.Find(Live & Builders<Item>.Filter.Eq(x => x.CodeReservationId, reservationId))
            .FirstOrDefaultAsync(cancellationToken)!;

    public Task<bool> NameKeyTakenAsync(string nameKey, Guid? exceptItemId, CancellationToken cancellationToken = default)
    {
        var filter = Live & Builders<Item>.Filter.Eq(x => x.NameKey, nameKey);
        if (exceptItemId is { } except) filter &= Builders<Item>.Filter.Ne(x => x.Id, except);
        return _items.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<ItemPage> GetPageAsync(ItemPageRequest request, CancellationToken cancellationToken = default)
    {
        var filter = Live;
        if (request.Query is { } q)
        {
            var escaped = Regex.Escape(q);
            filter &= Builders<Item>.Filter.Regex(x => x.CanonicalCode, new BsonRegularExpression("^" + escaped, "i"))
                      | Builders<Item>.Filter.Regex(x => x.Name, new BsonRegularExpression(escaped, "i"));
        }
        if (request.MaterialType is { } type) filter &= Builders<Item>.Filter.Eq(x => x.MaterialType, type);
        if (request.Status is { } status) filter &= Builders<Item>.Filter.Eq(x => x.LifecycleStatus, status);
        var total = await _items.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _items.Find(filter)
            .SortBy(x => x.CanonicalCode)
            .Skip((request.Page - 1) * request.PageSize)
            .Limit(request.PageSize)
            .ToListAsync(cancellationToken);
        return new ItemPage(items, total);
    }

    public async Task<ItemWriteResult> CreateDraftAsync(Item item, CancellationToken cancellationToken = default)
    {
        if (await GetByCreationCommandIdAsync(item.CreationCommandId, cancellationToken) is { } existing)
            return existing.IsDeleted ? new(false, existing, "CREATION_COMMAND_TOMBSTONED")
                : SameFacts(existing, item) ? new(true, existing)
                : new(false, existing, "IDEMPOTENCY_KEY_CONFLICT");

        if (item.Id == Guid.Empty || item.CodeReservationId == Guid.Empty || string.IsNullOrWhiteSpace(item.CanonicalCode)
            || string.IsNullOrWhiteSpace(item.CreationCommandId) || item.LifecycleStatus != ItemLifecycleStatus.Draft
            || string.IsNullOrWhiteSpace(item.NameKey))
            return new(false, null, "ITEM_CONTRACT_INVALID");

        var reservationFilter = Builders<CodeReservation>.Filter.Eq(x => x.TenantId, _tenantId)
            & Builders<CodeReservation>.Filter.Eq(x => x.IsDeleted, false)
            & Builders<CodeReservation>.Filter.Eq(x => x.Id, item.CodeReservationId)
            & Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.Item)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservationState, CodeReservationState.Consumed)
            & Builders<CodeReservation>.Filter.Eq(x => x.ConsumedEntityId, item.Id)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservedCode, item.CanonicalCode)
            & Builders<CodeReservation>.Filter.In(x => x.BindingState,
                [CodeReservationBindingState.PendingIdentityWrite, CodeReservationBindingState.Confirmed]);
        if (!await _reservations.Find(reservationFilter).AnyAsync(cancellationToken))
            return new(false, null, "CODE_RESERVATION_REQUIRED");

        item.TenantId = _tenantId;
        item.IsDeleted = false;
        item.DeletedAt = null;
        item.Version = 0;

        try
        {
            // The LSKU / FG InsertBoundToLiveCodeAsync pattern (CT decision 1): a burn reads "no live item bound" in its
            // own transaction; this insert writes the reservation in ITS transaction, so the two fail each other — never
            // a live item with a burned code. A transient error is retried with backoff; running out is ambiguous (202).
            for (var attempt = 1; ; attempt++)
            {
                var inserted = await InsertBoundToLiveCodeAsync(item, reservationFilter, cancellationToken);
                if (inserted.ErrorCode != InsertConflictRetry) return inserted;
                if (attempt >= InsertAttempts) return AmbiguousWrite();
                await Task.Delay(TimeSpan.FromMilliseconds(50 << attempt), cancellationToken);
            }
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // FIX1 M2: first "is it this command's own row?" (a replay), only then a name conflict.
            var stored = await GetByCreationCommandIdAsync(item.CreationCommandId, cancellationToken)
                         ?? await GetByReservationIdAsync(item.CodeReservationId, cancellationToken);
            if (stored is not null && !stored.IsDeleted && SameFacts(stored, item)) return new(true, stored);
            return IsNameKeyConflict(exception)
                ? new(false, null, "ITEM_NAME_CONFLICT")
                : new(false, stored, "ITEM_DUPLICATE_CONFLICT");
        }
        catch (MongoConnectionException) { return AmbiguousWrite(); }
        catch (MongoExecutionTimeoutException) { return AmbiguousWrite(); }
        catch (MongoWriteConcernException) { return AmbiguousWrite(); }
        catch (MongoException exception) when (exception.HasErrorLabel("UnknownTransactionCommitResult"))
        {
            return AmbiguousWrite();
        }
        catch (TimeoutException) { return AmbiguousWrite(); }
    }

    internal const int InsertAttempts = 4;
    private const string InsertConflictRetry = "ITEM_INSERT_CONFLICT_RETRY";

    // Tests only (the FG / LSKU twin): a barrier inside the insert's transaction, before its reservation write.
    internal Func<Task>? BeforeReservationWriteForTests { get; set; }

    private async Task<ItemWriteResult> InsertBoundToLiveCodeAsync(Item item,
        FilterDefinition<CodeReservation> reservationFilter, CancellationToken cancellationToken)
    {
        using var session = await _items.Database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        try
        {
            session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority));
            if (BeforeReservationWriteForTests is { } barrier) await barrier();
            // UpdatedAt only: metadata outside the reservation's Version and audit chain; it makes an overlapping burn
            // and this insert conflict.
            var touched = await _reservations.UpdateOneAsync(session,
                reservationFilter & Builders<CodeReservation>.Filter.Ne(x => x.BindingState, CodeReservationBindingState.Burned),
                Builders<CodeReservation>.Update.Set(x => x.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken: cancellationToken);
            if (touched.MatchedCount != 1)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(false, null, "CODE_RESERVATION_BURNED");
            }
            await _items.InsertOneAsync(session, item, cancellationToken: cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            return new(true, item);
        }
        catch (Exception exception) when (exception is NotSupportedException || exception is MongoCommandException { Code: 20 })
        {
            // Without transactions (a standalone mongod) no burn can happen at all (it is refused): the plain insert.
            await _items.InsertOneAsync(item, cancellationToken: cancellationToken);
            return new(true, item);
        }
        catch (MongoException exception) when (exception.HasErrorLabel("TransientTransactionError")
                                               && exception is not MongoWriteException)
        {
            // FIX1 M1: tenant-bounded like every other read here.
            var code = await _reservations.Find(Builders<CodeReservation>.Filter.Eq(x => x.TenantId, _tenantId)
                    & Builders<CodeReservation>.Filter.Eq(x => x.Id, item.CodeReservationId))
                .FirstOrDefaultAsync(cancellationToken);
            return code?.BindingState == CodeReservationBindingState.Burned
                ? new(false, null, "CODE_RESERVATION_BURNED")
                : new(false, null, InsertConflictRetry);
        }
    }

    public async Task<ItemWriteResult> ReplaceAsync(Item item, int expectedVersion, CancellationToken cancellationToken = default)
    {
        item.TenantId = _tenantId;
        item.Version = expectedVersion + 1;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        try
        {
            var replaced = await _items.ReplaceOneAsync(
                Live & Builders<Item>.Filter.Eq(x => x.Id, item.Id) & Builders<Item>.Filter.Eq(x => x.Version, expectedVersion),
                item, cancellationToken: cancellationToken);
            return replaced.MatchedCount == 1
                ? new(true, item)
                : new(false, null, "ITEM_CONCURRENCY_CONFLICT");
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return IsNameKeyConflict(exception)
                ? new(false, null, "ITEM_NAME_CONFLICT")
                : new(false, null, "ITEM_DUPLICATE_CONFLICT");
        }
        catch (MongoConnectionException) { return AmbiguousWrite(); }
        catch (MongoExecutionTimeoutException) { return AmbiguousWrite(); }
        catch (MongoWriteConcernException) { return AmbiguousWrite(); }
        catch (TimeoutException) { return AmbiguousWrite(); }
    }

    private static bool IsNameKeyConflict(MongoWriteException exception) =>
        exception.WriteError?.Message?.Contains(NameKeyIndexName, StringComparison.Ordinal) == true;

    private static ItemWriteResult AmbiguousWrite() => new(false, null, "ITEM_WRITE_OUTCOME_AMBIGUOUS", WriteOutcomeAmbiguous: true);

    private static bool SameFacts(Item left, Item right) =>
        left.Id == right.Id
        && left.CodeReservationId == right.CodeReservationId
        && string.Equals(left.CanonicalCode, right.CanonicalCode, StringComparison.Ordinal)
        && string.Equals(left.CreationCommandId, right.CreationCommandId, StringComparison.Ordinal)
        && string.Equals(left.CreationFingerprint, right.CreationFingerprint, StringComparison.Ordinal);

    private void EnsureIndexes()
    {
        _items.Indexes.CreateMany([
            new CreateIndexModel<Item>(
                Builders<Item>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CanonicalCode),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_items_tenant_code" }),
            new CreateIndexModel<Item>(
                Builders<Item>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CodeReservationId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_items_tenant_reservation" }),
            new CreateIndexModel<Item>(
                Builders<Item>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CreationCommandId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_items_tenant_command" }),
            // §12: the name is unique per tenant + material type among items that are not Retired (NameKey is null once
            // Retired). Only string keys are indexed, so a retired or legacy document without a key holds nothing.
            new CreateIndexModel<Item>(
                Builders<Item>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.NameKey),
                new CreateIndexOptions<Item>
                {
                    Unique = true,
                    Name = NameKeyIndexName,
                    PartialFilterExpression = Builders<Item>.Filter.Type(x => x.NameKey, BsonType.String)
                }),
            new CreateIndexModel<Item>(
                Builders<Item>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.LifecycleStatus).Ascending(x => x.CanonicalCode),
                new CreateIndexOptions { Name = "ix_mdm_items_tenant_status_code" })
        ]);
    }
}
