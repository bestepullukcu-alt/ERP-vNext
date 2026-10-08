using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

// Internal only. The authoritative actor/scope adapter is intentionally fail-closed in production.
public sealed class PublishedRevisionMongoStore(
    DemandPlanningMongoContext context, ManualDraftMongoStore drafts,
    IInternalPublishAuthority authority) : IInternalPublishedRevisionStore
{
    private const int SafeDocumentBytes = 15_000_000;
    private readonly SemaphoreSlim _indexGate = new(1, 1);
    private bool _indexesReady;

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        if (_indexesReady) return;
        await _indexGate.WaitAsync(cancellationToken);
        try
        {
            if (_indexesReady) return;
            await drafts.EnsureIndexesAsync(cancellationToken);
            var manifestKeys = Builders<PublishedRevisionManifest>.IndexKeys;
            await context.PublishedRevisionManifests.Indexes.CreateOneAsync(
                new CreateIndexModel<PublishedRevisionManifest>(manifestKeys
                    .Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.RevisionId), new CreateIndexOptions { Unique = true }),
                cancellationToken: cancellationToken);
            var partKeys = Builders<PublishedRevisionPart>.IndexKeys;
            await context.PublishedRevisionParts.Indexes.CreateOneAsync(
                new CreateIndexModel<PublishedRevisionPart>(partKeys
                    .Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.RevisionId).Ascending(x => x.SkuId)
                    .Ascending(x => x.WarehouseId), new CreateIndexOptions { Unique = true }),
                cancellationToken: cancellationToken);
            var slotKeys = Builders<PublishedBaselineSlot>.IndexKeys;
            await context.PublishedBaselineSlots.Indexes.CreateOneAsync(
                new CreateIndexModel<PublishedBaselineSlot>(slotKeys
                    .Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.PlanningPeriodKey), new CreateIndexOptions { Unique = true }),
                cancellationToken: cancellationToken);
            var auditKeys = Builders<ManualDraftPublicationAuditRecord>.IndexKeys;
            await context.ManualDraftPublicationAudit.Indexes.CreateManyAsync([
                new CreateIndexModel<ManualDraftPublicationAuditRecord>(auditKeys
                    .Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.RevisionId).Ascending(x => x.RequestKey),
                    new CreateIndexOptions { Unique = true }),
                new CreateIndexModel<ManualDraftPublicationAuditRecord>(auditKeys
                    .Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.RevisionId).Ascending(x => x.StateVersionAfter),
                    new CreateIndexOptions { Unique = true })
            ], cancellationToken);
            var outboxKeys = Builders<DemandOutboxMessage>.IndexKeys;
            await context.Outbox.Indexes.CreateOneAsync(new CreateIndexModel<DemandOutboxMessage>(
                outboxKeys.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.EventId), new CreateIndexOptions { Unique = true }),
                cancellationToken: cancellationToken);
            var generalAuditKeys = Builders<DemandAuditEntry>.IndexKeys;
            await context.AuditEntries.Indexes.CreateOneAsync(
                new CreateIndexModel<DemandAuditEntry>(generalAuditKeys
                    .Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Action)), cancellationToken: cancellationToken);
            _indexesReady = true;
        }
        finally { _indexGate.Release(); }
    }

    public async Task<PublishResult> PublishAsync(Guid tenantId, Guid legalEntityId,
        Guid revisionId, Guid actorId, string requestKey, int expectedContentVersion,
        int expectedStateVersion, DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty ||
            revisionId == Guid.Empty || actorId == Guid.Empty ||
            string.IsNullOrWhiteSpace(requestKey) || occurredAt == default ||
            expectedContentVersion < 0 || expectedStateVersion < 0)
            return new(PublishOutcome.Invalid);
        await EnsureIndexesAsync(cancellationToken);
        var key = requestKey.Trim();
        var fingerprint = Hash(JsonSerializer.Serialize(new
        {
            tenantId, legalEntityId, revisionId, actorId, key,
            expectedContentVersion, expectedStateVersion
        }));
        using var session = await context.StartSessionAsync(cancellationToken);
        session.StartTransaction(new TransactionOptions(
            readConcern: MongoDB.Driver.ReadConcern.Snapshot,
            writeConcern: MongoDB.Driver.WriteConcern.WMajority));
        try
        {
            var loaded = await drafts.LoadAsync(session, tenantId, legalEntityId,
                revisionId, cancellationToken);
            if (loaded is null)
                return await AbortResult(session, PublishOutcome.ScopeDenied, cancellationToken);
            var (manualManifest, draft) = loaded.Value;
            var scope = draft.Series.Where(x => x.Exclusion is null)
                .Select(x => (x.SkuId, x.WarehouseId)).ToArray();
            PublishAuthorityEvidence authorization;
            try
            {
                authorization = await authority.VerifyAsync(tenantId, legalEntityId,
                    actorId, scope, cancellationToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                return await AbortResult(session, PublishOutcome.AuthorityUnavailable,
                    cancellationToken);
            }
            if (!authorization.SourceAvailable)
                return await AbortResult(session, PublishOutcome.AuthorityUnavailable,
                    cancellationToken);
            if (authorization.IsIntegrationActor || !authorization.HasPublishPermission)
                return await AbortResult(session, PublishOutcome.PermissionDenied,
                    cancellationToken);
            if (!authorization.ScopeVerified)
                return await AbortResult(session, PublishOutcome.ScopeDenied,
                    cancellationToken);
            if (draft.HasSignificantContribution(actorId))
                return await AbortResult(session, PublishOutcome.SeparationDenied, cancellationToken);
            var previousRequest = await context.ManualDraftPublicationAudit.Find(session, x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.RevisionId == revisionId && x.RequestKey == key)
                .FirstOrDefaultAsync(cancellationToken);
            if (previousRequest is not null)
                return await AbortResult(session,
                    previousRequest.NewState == DemandRevisionState.Published &&
                    previousRequest.Fingerprint == fingerprint
                        ? PublishOutcome.Replayed : PublishOutcome.Conflict,
                    cancellationToken, revisionId);
            if (draft.State != DemandRevisionState.Approved ||
                draft.Version != expectedContentVersion ||
                draft.StateVersion != expectedStateVersion || draft.ReviewedBy is null)
                return await AbortResult(session, PublishOutcome.Conflict, cancellationToken);
            if (!draft.HasCompleteManualQuantities)
                return await AbortResult(session, PublishOutcome.Invalid, cancellationToken);
            var (published, parts) = PublishedSnapshotIntegrity.Build(draft, actorId, occurredAt);
            if (!draft.ApplyPublicationTransition(DemandRevisionState.Published,
                    actorId, expectedContentVersion, expectedStateVersion,
                    enforcePublisherSeparation: true))
                return await AbortResult(session, PublishOutcome.Conflict, cancellationToken);
            CheckDocument(published);
            foreach (var part in parts) CheckDocument(part);
            var period = draft.PlanningPeriodKey;
            var current = await context.PublishedBaselineSlots.Find(session, x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.PlanningPeriodKey == period && !x.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            if (current?.IsAvailable == true && current.RevisionId == revisionId)
                return await AbortResult(session, PublishOutcome.Conflict, cancellationToken);
            if (current?.IsAvailable == true)
            {
                var oldSnapshot = await ReadSnapshotAsync(session, tenantId, legalEntityId,
                    current.RevisionId, cancellationToken);
                var oldLoaded = await drafts.LoadAsync(session, tenantId, legalEntityId,
                    current.RevisionId, cancellationToken);
                if (oldSnapshot is null || oldLoaded is null ||
                    oldSnapshot.Value.Manifest.State != DemandRevisionState.Published ||
                    oldLoaded.Value.Draft.State != DemandRevisionState.Published ||
                    oldSnapshot.Value.Manifest.StateVersion != oldLoaded.Value.Draft.StateVersion)
                    throw new InvalidDataException("Current published baseline is incomplete.");
                var old = oldLoaded.Value;
                var oldVersion = old.Draft.StateVersion;
                if (!old.Draft.ApplyPublicationTransition(DemandRevisionState.Superseded,
                    actorId, old.Draft.Version, oldVersion,
                    enforcePublisherSeparation: false))
                    throw new InvalidDataException("Current baseline cannot be superseded.");
                var oldManualFilter = Builders<ManualDraftManifest>.Filter.Where(x =>
                    x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                    x.Id == current.RevisionId && x.State == DemandRevisionState.Published &&
                    x.StateVersion == oldVersion && !x.IsDeleted);
                old.Manifest.State = DemandRevisionState.Superseded;
                old.Manifest.StateVersion = oldVersion + 1;
                if ((await context.ManualDraftManifests.ReplaceOneAsync(session,
                        oldManualFilter, old.Manifest, cancellationToken: cancellationToken))
                    .ModifiedCount != 1)
                    return await AbortResult(session, PublishOutcome.Conflict, cancellationToken);
                var oldPublishedFilter = Builders<PublishedRevisionManifest>.Filter.Where(x =>
                    x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                    x.RevisionId == current.RevisionId &&
                    x.State == DemandRevisionState.Published &&
                    x.StateVersion == oldVersion && !x.IsDeleted);
                oldSnapshot.Value.Manifest.State = DemandRevisionState.Superseded;
                oldSnapshot.Value.Manifest.StateVersion = oldVersion + 1;
                if ((await context.PublishedRevisionManifests.ReplaceOneAsync(session,
                        oldPublishedFilter, oldSnapshot.Value.Manifest,
                        cancellationToken: cancellationToken)).ModifiedCount != 1)
                    return await AbortResult(session, PublishOutcome.Conflict, cancellationToken);
                var supersedeAudit = new ManualDraftPublicationAuditRecord
                {
                    TenantId = tenantId, LegalEntityId = legalEntityId,
                    RevisionId = current.RevisionId,
                    RequestKey = $"superseded-by:{revisionId:D}",
                    Fingerprint = Hash($"{tenantId:D}|{legalEntityId:D}|{current.RevisionId:D}|{revisionId:D}"),
                    NewState = DemandRevisionState.Superseded,
                    ContentVersion = old.Draft.Version, StateVersionAfter = oldVersion + 1,
                    ActorId = actorId, OccurredAt = occurredAt,
                    ReplacedByRevisionId = revisionId, CreatedAt = occurredAt
                };
                await context.ManualDraftPublicationAudit.InsertOneAsync(session,
                    supersedeAudit, cancellationToken: cancellationToken);
                await context.AuditEntries.InsertOneAsync(session, new DemandAuditEntry
                {
                    TenantId = tenantId, LegalEntityId = legalEntityId,
                    ActorId = actorId, Action = "Superseded",
                    EvidenceReference = current.RevisionId.ToString("D"),
                    OccurredAt = occurredAt, CreatedAt = occurredAt
                }, cancellationToken: cancellationToken);
            }
            var manualFilter = Builders<ManualDraftManifest>.Filter.Where(x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.Id == revisionId && x.State == DemandRevisionState.Approved &&
                x.Version == expectedContentVersion &&
                x.StateVersion == expectedStateVersion && !x.IsDeleted);
            manualManifest.State = draft.State;
            manualManifest.StateVersion = draft.StateVersion;
            if ((await context.ManualDraftManifests.ReplaceOneAsync(session,
                    manualFilter, manualManifest, cancellationToken: cancellationToken))
                .ModifiedCount != 1)
                return await AbortResult(session, PublishOutcome.Conflict, cancellationToken);
            var candidateSlotFilter = Builders<ManualDraftReviewSlot>.Filter.Where(x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.PlanningCycleId == draft.PlanningCycleId && x.RevisionId == revisionId &&
                !x.IsDeleted);
            var slotDelete = await context.ManualDraftReviewSlots.DeleteOneAsync(session,
                candidateSlotFilter, null, cancellationToken);
            if (slotDelete.DeletedCount != 1)
                throw new InvalidDataException("Approved candidate slot is absent.");
            await context.PublishedRevisionManifests.InsertOneAsync(session, published,
                cancellationToken: cancellationToken);
            await context.PublishedRevisionParts.InsertManyAsync(session, parts,
                cancellationToken: cancellationToken);
            if (current is null)
                await context.PublishedBaselineSlots.InsertOneAsync(session,
                    new PublishedBaselineSlot
                    {
                        TenantId = tenantId, LegalEntityId = legalEntityId,
                        PlanningPeriodKey = period, RevisionId = revisionId,
                        IsAvailable = true,
                        CreatedAt = occurredAt
                    }, cancellationToken: cancellationToken);
            else
            {
                var priorRevisionId = current.RevisionId;
                var priorSlotVersion = current.Version;
                var priorAvailability = current.IsAvailable;
                var slotFilter = Builders<PublishedBaselineSlot>.Filter.Where(x =>
                    x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                    x.PlanningPeriodKey == period && x.RevisionId == priorRevisionId &&
                    x.Version == priorSlotVersion &&
                    x.IsAvailable == priorAvailability && !x.IsDeleted);
                current.RevisionId = revisionId;
                current.IsAvailable = true;
                current.Version++;
                current.UpdatedAt = occurredAt;
                if ((await context.PublishedBaselineSlots.ReplaceOneAsync(session,
                        slotFilter, current, cancellationToken: cancellationToken))
                    .ModifiedCount != 1)
                    return await AbortResult(session, PublishOutcome.Conflict, cancellationToken);
            }
            await context.ManualDraftPublicationAudit.InsertOneAsync(session,
                new ManualDraftPublicationAuditRecord
                {
                    TenantId = tenantId, LegalEntityId = legalEntityId,
                    RevisionId = revisionId, RequestKey = key, Fingerprint = fingerprint,
                    NewState = DemandRevisionState.Published,
                    ContentVersion = expectedContentVersion,
                    StateVersionAfter = expectedStateVersion + 1,
                    ActorId = actorId, OccurredAt = occurredAt, CreatedAt = occurredAt
                }, cancellationToken: cancellationToken);
            await context.AuditEntries.InsertOneAsync(session, new DemandAuditEntry
            {
                TenantId = tenantId, LegalEntityId = legalEntityId,
                ActorId = actorId, Action = "Published",
                EvidenceReference = $"{revisionId:D}:{published.Checksum}",
                OccurredAt = occurredAt, CreatedAt = occurredAt
            }, cancellationToken: cancellationToken);
            var eventId = DeterministicEventId(tenantId, legalEntityId, revisionId);
            await context.Outbox.InsertOneAsync(session, new DemandOutboxMessage
            {
                TenantId = tenantId, LegalEntityId = legalEntityId,
                RevisionId = revisionId, EventId = eventId,
                EventType = "demand.revision.published.v2",
                StateVersion = published.StateVersion,
                Payload = DemandRevisionOutboxPayload.Published(published, eventId, occurredAt),
                OccurredAt = occurredAt, CreatedAt = occurredAt
            }, cancellationToken: cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            return new(PublishOutcome.Published, revisionId, published.Checksum,
                published.StateVersion);
        }
        catch (MongoException error) when (IsConflict(error))
        {
            await AbortIfActive(session, cancellationToken);
            var durable = await context.ManualDraftPublicationAudit.Find(x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.RevisionId == revisionId && x.RequestKey == key)
                .FirstOrDefaultAsync(cancellationToken);
            return new(durable?.NewState == DemandRevisionState.Published &&
                durable.Fingerprint == fingerprint
                    ? PublishOutcome.Replayed : PublishOutcome.Conflict, revisionId);
        }
        catch (InvalidDataException)
        {
            await AbortIfActive(session, cancellationToken);
            return new(PublishOutcome.InvalidSnapshot);
        }
        catch
        {
            await AbortIfActive(session, cancellationToken);
            throw;
        }
    }

    public async Task<(PublishedRevisionManifest Manifest, PublishedRevisionPart[] Parts)?>
        ReadSnapshotAsync(Guid tenantId, Guid legalEntityId, Guid revisionId,
            CancellationToken cancellationToken)
    {
        using var session = await context.StartSessionAsync(cancellationToken);
        session.StartTransaction(new TransactionOptions(readConcern: MongoDB.Driver.ReadConcern.Snapshot));
        try
        {
            var result = await ReadSnapshotAsync(session, tenantId, legalEntityId,
                revisionId, cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            return result;
        }
        catch
        {
            await AbortIfActive(session, cancellationToken);
            throw;
        }
    }

    internal async Task<(PublishedRevisionManifest Manifest, PublishedRevisionPart[] Parts)?>
        ReadSnapshotAsync(IClientSessionHandle session, Guid tenantId, Guid legalEntityId,
            Guid revisionId, CancellationToken cancellationToken)
    {
        var manifest = await context.PublishedRevisionManifests.Find(session, x =>
            x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
            x.RevisionId == revisionId && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        if (manifest is null) return null;
        var parts = (await context.PublishedRevisionParts.Find(session, x =>
            x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
            x.RevisionId == revisionId && !x.IsDeleted)
            .ToListAsync(cancellationToken)).ToArray();
        if (manifest.ChecksumScheme != "MOD0188-CANONICAL-1/SHA-256" ||
            manifest.Checksum != PublishedSnapshotIntegrity.Calculate(manifest, parts))
            throw new InvalidDataException("Published revision checksum or parts are invalid.");
        var manual = await drafts.LoadAsync(session, tenantId, legalEntityId,
            revisionId, cancellationToken);
        if (manual is null || manual.Value.Draft.State != manifest.State ||
            (manifest.PreparedAt != default &&
                manual.Value.Draft.CreatedAt != manifest.PreparedAt) ||
            manual.Value.Draft.CreatedBy != manifest.CreatedBy ||
            manual.Value.Draft.StateVersion != manifest.StateVersion ||
            manual.Value.Draft.Version != manifest.ContentVersion ||
            manual.Value.Draft.PlanningCycleId != manifest.PlanningCycleId ||
            manual.Value.Draft.PlanningPeriodKey != manifest.PlanningPeriodKey)
            throw new InvalidDataException("Published revision lifecycle and draft audit disagree.");
        // Legacy BSON had no PreparedAt. The Draft loader has already verified
        // its durable Created audit; hydrate only the read result, never Mongo.
        if (manifest.PreparedAt == default)
            manifest.PreparedAt = manual.Value.Draft.CreatedAt;
        return (manifest, parts);
    }

    private static async Task<PublishResult> AbortResult(IClientSessionHandle session,
        PublishOutcome outcome, CancellationToken cancellationToken, Guid? revisionId = null)
    {
        await AbortIfActive(session, cancellationToken);
        return new(outcome, revisionId);
    }

    private static async Task AbortIfActive(IClientSessionHandle session,
        CancellationToken cancellationToken)
    {
        if (session.IsInTransaction)
            await session.AbortTransactionAsync(cancellationToken);
    }

    private static bool IsConflict(MongoException error) =>
        error.HasErrorLabel("TransientTransactionError") ||
        error is MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey };

    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static Guid DeterministicEventId(Guid tenantId, Guid legalEntityId, Guid revisionId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"MOD0188-PUBLISHED|{tenantId:D}|{legalEntityId:D}|{revisionId:D}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static void CheckDocument<T>(T value) where T : EntityBase
    {
        if (value.ToBson().Length > SafeDocumentBytes)
            throw new InvalidDataException("A published revision physical part exceeds safe BSON size.");
    }
}
