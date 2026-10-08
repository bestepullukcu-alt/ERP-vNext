using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

// Each series is a physical part; the manifest, all parts and mandatory audit commit together.
// This store is internal only. No route or history-sufficiency decision is exposed here.
public sealed partial class ManualDraftMongoStore(DemandPlanningMongoContext context) : IManualDraftStore
{
    private const int SafeDocumentBytes = 15_000_000; // Below MongoDB's 16 MiB BSON ceiling.
    private static readonly JsonSerializerOptions JsonOptions = new();
    private readonly SemaphoreSlim _indexGate = new(1, 1);
    private bool _indexReady;

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        if (_indexReady) return;
        await _indexGate.WaitAsync(cancellationToken);
        try
        {
            if (_indexReady) return;
            var manifests = Builders<ManualDraftManifest>.IndexKeys;
            await context.ManualDraftManifests.Indexes.CreateManyAsync([
                new CreateIndexModel<ManualDraftManifest>(
                    manifests.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId)
                        .Ascending(x => x.Id)),
                new CreateIndexModel<ManualDraftManifest>(
                    manifests.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId)
                        .Ascending(x => x.PlanningCycleId).Ascending(x => x.CreateRequestKey),
                    new CreateIndexOptions { Unique = true })
            ], cancellationToken);
            var parts = Builders<ManualDraftSeriesPart>.IndexKeys;
            await context.ManualDraftSeriesParts.Indexes.CreateOneAsync(
                new CreateIndexModel<ManualDraftSeriesPart>(
                    parts.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId)
                        .Ascending(x => x.RevisionId).Ascending(x => x.SkuId)
                        .Ascending(x => x.WarehouseId),
                    new CreateIndexOptions { Unique = true }), cancellationToken: cancellationToken);
            var audit = Builders<ManualDraftAuditRecord>.IndexKeys;
            await context.ManualDraftAudit.Indexes.CreateManyAsync([
                new CreateIndexModel<ManualDraftAuditRecord>(
                    audit.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId)
                        .Ascending(x => x.RevisionId).Ascending(x => x.RequestKey),
                    new CreateIndexOptions { Unique = true }),
                new CreateIndexModel<ManualDraftAuditRecord>(
                    audit.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId)
                        .Ascending(x => x.RevisionId).Ascending(x => x.VersionAfter))
            ], cancellationToken);
            var reviewAudit = Builders<ManualDraftReviewAuditRecord>.IndexKeys;
            await context.ManualDraftReviewAudit.Indexes.CreateManyAsync([
                new CreateIndexModel<ManualDraftReviewAuditRecord>(
                    reviewAudit.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId)
                        .Ascending(x => x.RevisionId).Ascending(x => x.RequestKey),
                    new CreateIndexOptions { Unique = true }),
                new CreateIndexModel<ManualDraftReviewAuditRecord>(
                    reviewAudit.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId)
                        .Ascending(x => x.RevisionId).Ascending(x => x.StateVersionAfter),
                    new CreateIndexOptions { Unique = true })
            ], cancellationToken);
            var slots = Builders<ManualDraftReviewSlot>.IndexKeys;
            await context.ManualDraftReviewSlots.Indexes.CreateOneAsync(
                new CreateIndexModel<ManualDraftReviewSlot>(
                    slots.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId)
                        .Ascending(x => x.PlanningCycleId),
                    new CreateIndexOptions { Unique = true }), cancellationToken: cancellationToken);
            _indexReady = true;
        }
        finally
        {
            _indexGate.Release();
        }
    }

    public async Task<ManualDraftStoreResult> CreateAsync(
        DemandRevisionDraft draft, string requestKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.TenantId == Guid.Empty || draft.LegalEntityId == Guid.Empty ||
            draft.PlanningCycleId == Guid.Empty || draft.CreatedBy == Guid.Empty ||
            draft.State != DemandRevisionState.Draft || draft.Version != 0 ||
            draft.Changes.Count != 0 || draft.Series.Count == 0 ||
            string.IsNullOrWhiteSpace(requestKey))
            return new(ManualDraftStoreOutcome.Invalid);

        await EnsureIndexesAsync(cancellationToken);

        var cycle = CycleFromDraft(draft);
        var cycleJson = JsonSerializer.Serialize(cycle, JsonOptions);
        var seriesJson = draft.Series.Select(series => JsonSerializer.Serialize(
            new VerifiedDraftSeries(series.SkuId, series.WarehouseId, series.BaseUomId,
                series.Weeks.Select(week => new ManualDraftWeekInput(week.Number,
                    week.WeekStart, week.WeekEnd, week.ValueKind, week.Quantity)).ToArray()),
            JsonOptions)).ToArray();
        var normalizedKey = requestKey.Trim();
        var fingerprint = Hash(cycleJson + "\n" + string.Join("\n", seriesJson) + "\n" +
            draft.CreatedBy + "\n" + draft.CreationReason);
        var manifest = new ManualDraftManifest
        {
            Id = draft.Id, TenantId = draft.TenantId, LegalEntityId = draft.LegalEntityId,
            PlanningCycleId = draft.PlanningCycleId, CycleJson = cycleJson,
            CreateRequestKey = normalizedKey, CreateFingerprint = fingerprint,
            CreationReason = draft.CreationReason, CreatedBy = draft.CreatedBy,
            CreatedAt = draft.CreatedAt, ExpectedPartCount = seriesJson.Length
        };
        var parts = draft.Series.Zip(seriesJson, (series, json) => new ManualDraftSeriesPart
        {
            TenantId = draft.TenantId, LegalEntityId = draft.LegalEntityId,
            RevisionId = draft.Id, SkuId = series.SkuId,
            WarehouseId = series.WarehouseId, InitialSeriesJson = json,
            CreatedAt = draft.CreatedAt
        }).ToArray();
        var audit = new ManualDraftAuditRecord
        {
            TenantId = draft.TenantId, LegalEntityId = draft.LegalEntityId,
            RevisionId = draft.Id, RequestKey = normalizedKey,
            Fingerprint = fingerprint, Action = "Created", VersionAfter = 0,
            ActorId = draft.CreatedBy, OccurredAt = draft.CreatedAt,
            Reason = draft.CreationReason, CreatedAt = draft.CreatedAt
        };
        EnsureDocumentFits(manifest);
        EnsureDocumentFits(audit);
        foreach (var part in parts) EnsureDocumentFits(part);

        using var session = await context.StartSessionAsync(cancellationToken);
        session.StartTransaction();
        try
        {
            await context.ManualDraftManifests.InsertOneAsync(session, manifest,
                cancellationToken: cancellationToken);
            await context.ManualDraftSeriesParts.InsertManyAsync(session, parts,
                cancellationToken: cancellationToken);
            await context.ManualDraftAudit.InsertOneAsync(session, audit,
                cancellationToken: cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            return new(ManualDraftStoreOutcome.Created, draft);
        }
        catch (MongoException error) when (IsWriteConflict(error))
        {
            await AbortIfActiveAsync(session, cancellationToken);
            var existing = await context.ManualDraftManifests.Find(x =>
                x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
                x.PlanningCycleId == draft.PlanningCycleId && x.CreateRequestKey == normalizedKey)
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is null || existing.CreateFingerprint != fingerprint)
                return new(ManualDraftStoreOutcome.Conflict);
            return new(ManualDraftStoreOutcome.Replayed,
                await ReadAsync(draft.TenantId, draft.LegalEntityId, existing.Id, cancellationToken));
        }
        catch
        {
            await AbortIfActiveAsync(session, cancellationToken);
            throw;
        }
    }

    public async Task<ManualDraftStoreResult> EditWeekAsync(
        Guid tenantId, Guid legalEntityId, Guid revisionId, Guid actorId,
        Guid skuId, string warehouseId, int weekNumber,
        DraftWeekValueKind kind, decimal? quantity, string reason,
        string requestKey, int expectedVersion, DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty || revisionId == Guid.Empty ||
            actorId == Guid.Empty || skuId == Guid.Empty || string.IsNullOrWhiteSpace(warehouseId) ||
            string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(requestKey) ||
            !DemandRevisionDraft.IsValidValue(kind, quantity) || occurredAt == default)
            return new(ManualDraftStoreOutcome.Invalid);
        await EnsureIndexesAsync(cancellationToken);
        var normalizedKey = requestKey.Trim();
        var normalizedReason = reason.Trim();
        var fingerprint = Hash(JsonSerializer.Serialize(new
        {
            actorId, skuId, warehouseId, weekNumber, kind, quantity,
            reason = normalizedReason
        }, JsonOptions));
        using var session = await context.StartSessionAsync(cancellationToken);
        session.StartTransaction();
        try
        {
            var existingAudit = await context.ManualDraftAudit.Find(session, x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.RevisionId == revisionId && x.RequestKey == normalizedKey)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingAudit is not null)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(existingAudit.Action == "Edited" && existingAudit.Fingerprint == fingerprint
                    ? ManualDraftStoreOutcome.Replayed : ManualDraftStoreOutcome.Conflict);
            }
            var loaded = await LoadAsync(session, tenantId, legalEntityId, revisionId, cancellationToken);
            if (loaded is null)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(ManualDraftStoreOutcome.ScopeDenied);
            }
            var (manifest, draft) = loaded.Value;
            var outcome = draft.EditManualWeek(tenantId, legalEntityId, actorId,
                skuId, warehouseId, weekNumber, kind, quantity, normalizedReason,
                normalizedKey, expectedVersion, occurredAt);
            if (outcome != DraftEditOutcome.Changed)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(outcome switch
                {
                    DraftEditOutcome.Invalid => ManualDraftStoreOutcome.Invalid,
                    DraftEditOutcome.ScopeDenied => ManualDraftStoreOutcome.ScopeDenied,
                    DraftEditOutcome.Replayed => ManualDraftStoreOutcome.Replayed,
                    _ => ManualDraftStoreOutcome.Conflict
                });
            }
            var change = draft.Changes[^1];
            var audit = new ManualDraftAuditRecord
            {
                TenantId = tenantId, LegalEntityId = legalEntityId, RevisionId = revisionId,
                RequestKey = normalizedKey, Fingerprint = fingerprint, Action = "Edited",
                VersionAfter = draft.Version, ActorId = actorId, OccurredAt = occurredAt,
                Reason = normalizedReason,
                ChangeJson = JsonSerializer.Serialize(change, JsonOptions),
                CreatedAt = occurredAt
            };
            EnsureDocumentFits(audit);
            var filter = Builders<ManualDraftManifest>.Filter.Where(x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.Id == revisionId && x.Version == expectedVersion &&
                x.State == DemandRevisionState.Draft && !x.IsDeleted);
            manifest.Version = draft.Version;
            manifest.UpdatedBy = actorId;
            manifest.UpdatedAt = occurredAt;
            var replaced = await context.ManualDraftManifests.ReplaceOneAsync(
                session, filter, manifest, cancellationToken: cancellationToken);
            if (replaced.ModifiedCount != 1)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(ManualDraftStoreOutcome.Conflict);
            }
            await context.ManualDraftAudit.InsertOneAsync(session, audit,
                cancellationToken: cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            return new(ManualDraftStoreOutcome.Changed, draft);
        }
        catch (MongoException error) when (IsWriteConflict(error))
        {
            await AbortIfActiveAsync(session, cancellationToken);
            // A retry after a concurrent commit is resolved by the durable request key.
            var durable = await context.ManualDraftAudit.Find(x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.RevisionId == revisionId && x.RequestKey == normalizedKey)
                .FirstOrDefaultAsync(cancellationToken);
            return new(durable?.Action == "Edited" && durable.Fingerprint == fingerprint
                ? ManualDraftStoreOutcome.Replayed : ManualDraftStoreOutcome.Conflict);
        }
        catch
        {
            await AbortIfActiveAsync(session, cancellationToken);
            throw;
        }
    }

    public async Task<ManualDraftStoreResult> ExcludeSeriesAsync(
        Guid tenantId, Guid legalEntityId, Guid revisionId, Guid actorId,
        Guid skuId, string warehouseId, string reason, string requestKey,
        int expectedContentVersion, int expectedStateVersion,
        DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty || revisionId == Guid.Empty ||
            actorId == Guid.Empty || skuId == Guid.Empty ||
            string.IsNullOrWhiteSpace(warehouseId) || string.IsNullOrWhiteSpace(reason) ||
            string.IsNullOrWhiteSpace(requestKey) || occurredAt == default ||
            expectedContentVersion < 0 || expectedStateVersion < 0)
            return new(ManualDraftStoreOutcome.Invalid);
        await EnsureIndexesAsync(cancellationToken);
        var key = requestKey.Trim();
        var normalizedReason = reason.Trim();
        var fingerprint = Hash(JsonSerializer.Serialize(new
        {
            actorId, skuId, warehouseId, reason = normalizedReason,
            expectedContentVersion, expectedStateVersion
        }, JsonOptions));
        using var session = await context.StartSessionAsync(cancellationToken);
        session.StartTransaction();
        try
        {
            var previous = await context.ManualDraftAudit.Find(session, x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.RevisionId == revisionId && x.RequestKey == key)
                .FirstOrDefaultAsync(cancellationToken);
            if (previous is not null)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(previous.Action == "Excluded" && previous.Fingerprint == fingerprint
                    ? ManualDraftStoreOutcome.Replayed : ManualDraftStoreOutcome.Conflict);
            }
            var conflictingReviewKey = await context.ManualDraftReviewAudit.Find(session, x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.RevisionId == revisionId && x.RequestKey == key)
                .AnyAsync(cancellationToken);
            if (conflictingReviewKey)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(ManualDraftStoreOutcome.Conflict);
            }
            var loaded = await LoadAsync(session, tenantId, legalEntityId,
                revisionId, cancellationToken);
            if (loaded is null)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(ManualDraftStoreOutcome.ScopeDenied);
            }
            var (manifest, draft) = loaded.Value;
            var previousState = draft.State;
            if (draft.Version != expectedContentVersion ||
                draft.StateVersion != expectedStateVersion ||
                previousState is not (DemandRevisionState.Draft or DemandRevisionState.InReview))
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(ManualDraftStoreOutcome.Conflict);
            }
            ManualDraftReviewAuditRecord? reopenAudit = null;
            if (previousState == DemandRevisionState.InReview)
            {
                if (draft.ApplyReviewTransition(DraftReviewAction.Reopened, actorId,
                        normalizedReason, expectedContentVersion, expectedStateVersion,
                        occurredAt) != DraftReviewOutcome.Changed)
                {
                    await session.AbortTransactionAsync(cancellationToken);
                    return new(ManualDraftStoreOutcome.Conflict);
                }
                var reviewFingerprint = Hash(JsonSerializer.Serialize(new
                {
                    actorId, action = DraftReviewAction.Reopened,
                    reason = normalizedReason, expectedContentVersion,
                    expectedStateVersion
                }, JsonOptions));
                reopenAudit = new ManualDraftReviewAuditRecord
                {
                    TenantId = tenantId, LegalEntityId = legalEntityId,
                    RevisionId = revisionId, RequestKey = key,
                    Fingerprint = reviewFingerprint, Action = DraftReviewAction.Reopened,
                    ContentVersion = expectedContentVersion,
                    StateVersionAfter = draft.StateVersion, ActorId = actorId,
                    OccurredAt = occurredAt, Reason = normalizedReason,
                    CreatedAt = occurredAt
                };
                EnsureDocumentFits(reopenAudit);
            }
            var outcome = draft.ExcludeSeries(tenantId, legalEntityId, actorId,
                skuId, warehouseId, normalizedReason, key,
                expectedContentVersion, occurredAt, previousState);
            if (outcome != DraftEditOutcome.Changed)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(outcome == DraftEditOutcome.Invalid
                    ? ManualDraftStoreOutcome.Invalid : ManualDraftStoreOutcome.Conflict);
            }
            var exclusion = draft.Exclusions[^1];
            var audit = new ManualDraftAuditRecord
            {
                TenantId = tenantId, LegalEntityId = legalEntityId,
                RevisionId = revisionId, RequestKey = key, Fingerprint = fingerprint,
                Action = "Excluded", VersionAfter = draft.Version,
                ActorId = actorId, OccurredAt = occurredAt,
                Reason = normalizedReason,
                ChangeJson = JsonSerializer.Serialize(exclusion, JsonOptions),
                CreatedAt = occurredAt
            };
            EnsureDocumentFits(audit);
            if (reopenAudit is not null)
            {
                var slotFilter = Builders<ManualDraftReviewSlot>.Filter.Where(x =>
                    x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                    x.PlanningCycleId == manifest.PlanningCycleId &&
                    x.RevisionId == revisionId && !x.IsDeleted);
                if ((await context.ManualDraftReviewSlots.DeleteOneAsync(session,
                        slotFilter, null, cancellationToken)).DeletedCount != 1)
                {
                    await session.AbortTransactionAsync(cancellationToken);
                    return new(ManualDraftStoreOutcome.Conflict);
                }
            }
            var filter = Builders<ManualDraftManifest>.Filter.Where(x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.Id == revisionId && x.Version == expectedContentVersion &&
                x.StateVersion == expectedStateVersion &&
                x.State == previousState && !x.IsDeleted);
            manifest.Version = draft.Version;
            manifest.State = draft.State;
            manifest.StateVersion = draft.StateVersion;
            manifest.ReviewedBy = draft.ReviewedBy;
            manifest.ReviewedAt = draft.ReviewedAt;
            manifest.ReviewReason = draft.ReviewReason;
            manifest.UpdatedBy = actorId;
            manifest.UpdatedAt = occurredAt;
            if ((await context.ManualDraftManifests.ReplaceOneAsync(session,
                    filter, manifest, cancellationToken: cancellationToken)).ModifiedCount != 1)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(ManualDraftStoreOutcome.Conflict);
            }
            if (reopenAudit is not null)
                await context.ManualDraftReviewAudit.InsertOneAsync(session,
                    reopenAudit, cancellationToken: cancellationToken);
            await context.ManualDraftAudit.InsertOneAsync(session, audit,
                cancellationToken: cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            return new(ManualDraftStoreOutcome.Changed, draft);
        }
        catch (MongoException error) when (IsWriteConflict(error))
        {
            await AbortIfActiveAsync(session, cancellationToken);
            var durable = await context.ManualDraftAudit.Find(x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.RevisionId == revisionId && x.RequestKey == key)
                .FirstOrDefaultAsync(cancellationToken);
            return new(durable?.Action == "Excluded" && durable.Fingerprint == fingerprint
                ? ManualDraftStoreOutcome.Replayed : ManualDraftStoreOutcome.Conflict);
        }
        catch
        {
            await AbortIfActiveAsync(session, cancellationToken);
            throw;
        }
    }

    public async Task<ManualDraftStoreResult> TransitionReviewAsync(
        Guid tenantId, Guid legalEntityId, Guid revisionId, Guid actorId,
        DraftReviewAction action, string? reason, string requestKey,
        int expectedContentVersion, int expectedStateVersion,
        DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty || revisionId == Guid.Empty ||
            actorId == Guid.Empty || !Enum.IsDefined(action) ||
            string.IsNullOrWhiteSpace(requestKey) || occurredAt == default ||
            expectedContentVersion < 0 || expectedStateVersion < 0 ||
            (action != DraftReviewAction.Submitted && string.IsNullOrWhiteSpace(reason)))
            return new(ManualDraftStoreOutcome.Invalid);
        await EnsureIndexesAsync(cancellationToken);
        var key = requestKey.Trim();
        var normalizedReason = reason?.Trim();
        var fingerprint = Hash(JsonSerializer.Serialize(new
        {
            actorId, action, reason = normalizedReason,
            expectedContentVersion, expectedStateVersion
        }, JsonOptions));
        using var session = await context.StartSessionAsync(cancellationToken);
        session.StartTransaction();
        try
        {
            var previous = await context.ManualDraftReviewAudit.Find(session, x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.RevisionId == revisionId && x.RequestKey == key)
                .FirstOrDefaultAsync(cancellationToken);
            if (previous is not null)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(previous.Fingerprint == fingerprint
                    ? ManualDraftStoreOutcome.Replayed : ManualDraftStoreOutcome.Conflict);
            }
            var loaded = await LoadAsync(session, tenantId, legalEntityId,
                revisionId, cancellationToken);
            if (loaded is null)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(ManualDraftStoreOutcome.ScopeDenied);
            }
            var (manifest, draft) = loaded.Value;
            var oldState = draft.State;
            var outcome = draft.ApplyReviewTransition(action, actorId, normalizedReason,
                expectedContentVersion, expectedStateVersion, occurredAt);
            if (outcome != DraftReviewOutcome.Changed)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(outcome switch
                {
                    DraftReviewOutcome.Invalid => ManualDraftStoreOutcome.Invalid,
                    DraftReviewOutcome.SeparationDenied => ManualDraftStoreOutcome.SeparationDenied,
                    _ => ManualDraftStoreOutcome.Conflict
                });
            }
            if (action == DraftReviewAction.Submitted)
            {
                await context.ManualDraftReviewSlots.InsertOneAsync(session,
                    new ManualDraftReviewSlot
                    {
                        TenantId = tenantId, LegalEntityId = legalEntityId,
                        PlanningCycleId = manifest.PlanningCycleId,
                        RevisionId = revisionId, CreatedAt = occurredAt
                    }, cancellationToken: cancellationToken);
            }
            else
            {
                var slotFilter = Builders<ManualDraftReviewSlot>.Filter.Where(x =>
                    x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                    x.PlanningCycleId == manifest.PlanningCycleId &&
                    x.RevisionId == revisionId && !x.IsDeleted);
                if (action == DraftReviewAction.Approved)
                {
                    if (await context.ManualDraftReviewSlots.CountDocumentsAsync(session,
                            slotFilter, cancellationToken: cancellationToken) != 1)
                    {
                        await session.AbortTransactionAsync(cancellationToken);
                        return new(ManualDraftStoreOutcome.Conflict);
                    }
                }
                else if ((await context.ManualDraftReviewSlots.DeleteOneAsync(session,
                             slotFilter, null, cancellationToken)).DeletedCount != 1)
                {
                    await session.AbortTransactionAsync(cancellationToken);
                    return new(ManualDraftStoreOutcome.Conflict);
                }
            }
            var manifestFilter = Builders<ManualDraftManifest>.Filter.Where(x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.Id == revisionId && x.Version == expectedContentVersion &&
                x.StateVersion == expectedStateVersion && x.State == oldState && !x.IsDeleted);
            manifest.State = draft.State;
            manifest.StateVersion = draft.StateVersion;
            manifest.ReviewedBy = draft.ReviewedBy;
            manifest.ReviewedAt = draft.ReviewedAt;
            manifest.ReviewReason = draft.ReviewReason;
            if ((await context.ManualDraftManifests.ReplaceOneAsync(session,
                    manifestFilter, manifest, cancellationToken: cancellationToken)).ModifiedCount != 1)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(ManualDraftStoreOutcome.Conflict);
            }
            var audit = new ManualDraftReviewAuditRecord
            {
                TenantId = tenantId, LegalEntityId = legalEntityId,
                RevisionId = revisionId, RequestKey = key, Fingerprint = fingerprint,
                Action = action, ContentVersion = expectedContentVersion,
                StateVersionAfter = draft.StateVersion, ActorId = actorId,
                OccurredAt = occurredAt, Reason = normalizedReason,
                CreatedAt = occurredAt
            };
            EnsureDocumentFits(audit);
            await context.ManualDraftReviewAudit.InsertOneAsync(session, audit,
                cancellationToken: cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            return new(ManualDraftStoreOutcome.Changed, draft);
        }
        catch (MongoException error) when (IsWriteConflict(error))
        {
            await AbortIfActiveAsync(session, cancellationToken);
            var durable = await context.ManualDraftReviewAudit.Find(x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.RevisionId == revisionId && x.RequestKey == key)
                .FirstOrDefaultAsync(cancellationToken);
            return new(durable?.Fingerprint == fingerprint
                ? ManualDraftStoreOutcome.Replayed : ManualDraftStoreOutcome.Conflict);
        }
        catch
        {
            await AbortIfActiveAsync(session, cancellationToken);
            throw;
        }
    }

    public async Task<DemandRevisionDraft?> ReadAsync(
        Guid tenantId, Guid legalEntityId, Guid revisionId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty || revisionId == Guid.Empty)
            return null;
        await EnsureIndexesAsync(cancellationToken);
        using var session = await context.StartSessionAsync(cancellationToken);
        session.StartTransaction(new TransactionOptions(readConcern: MongoDB.Driver.ReadConcern.Snapshot));
        try
        {
            var loaded = await LoadAsync(session, tenantId, legalEntityId,
                revisionId, cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            return loaded?.Draft;
        }
        catch
        {
            await AbortIfActiveAsync(session, cancellationToken);
            throw;
        }
    }

    internal async Task<(ManualDraftManifest Manifest, DemandRevisionDraft Draft)?> LoadAsync(
        IClientSessionHandle session, Guid tenantId, Guid legalEntityId,
        Guid revisionId, CancellationToken cancellationToken)
    {
        var manifest = await context.ManualDraftManifests.Find(session, x =>
            x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
            x.Id == revisionId && !x.IsDeleted).FirstOrDefaultAsync(cancellationToken);
        if (manifest is null) return null;
        var parts = await context.ManualDraftSeriesParts.Find(session, x =>
            x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
            x.RevisionId == revisionId && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        var audit = await context.ManualDraftAudit.Find(session, x =>
            x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
            x.RevisionId == revisionId && !x.IsDeleted)
            .SortBy(x => x.VersionAfter).ToListAsync(cancellationToken);
        var reviewAudit = await context.ManualDraftReviewAudit.Find(session, x =>
            x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
            x.RevisionId == revisionId && !x.IsDeleted)
            .SortBy(x => x.StateVersionAfter).ToListAsync(cancellationToken);
        var publicationAudit = await context.ManualDraftPublicationAudit.Find(session, x =>
            x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
            x.RevisionId == revisionId && !x.IsDeleted)
            .SortBy(x => x.StateVersionAfter).ToListAsync(cancellationToken);
        if (manifest.ExpectedPartCount < 1 || parts.Count != manifest.ExpectedPartCount ||
            parts.Select(x => (x.SkuId, x.WarehouseId)).Distinct().Count() != parts.Count ||
            audit.Count != manifest.Version + 1 ||
            audit.Count(x => x.Action == "Created" && x.VersionAfter == 0) != 1 ||
            reviewAudit.Count + publicationAudit.Count != manifest.StateVersion)
            throw new InvalidDataException("Manual draft parts or audit are incomplete.");
        var creation = audit[0];
        if (string.IsNullOrWhiteSpace(manifest.CreateRequestKey) ||
            string.IsNullOrWhiteSpace(manifest.CreateFingerprint) ||
            manifest.CreatedBy == Guid.Empty ||
            string.IsNullOrWhiteSpace(manifest.CreationReason) ||
            manifest.CreatedAt == default || creation.Action != "Created" ||
            creation.VersionAfter != 0 || creation.TenantId != tenantId ||
            creation.LegalEntityId != legalEntityId || creation.RevisionId != revisionId ||
            creation.RequestKey != manifest.CreateRequestKey ||
            creation.Fingerprint != manifest.CreateFingerprint ||
            creation.ActorId != manifest.CreatedBy ||
            creation.Reason != manifest.CreationReason ||
            creation.OccurredAt != manifest.CreatedAt ||
            creation.CreatedAt != manifest.CreatedAt || creation.ChangeJson is not null ||
            creation.SourceRevisionId != manifest.SourceRevisionId ||
            creation.SourceState != manifest.SourceState ||
            creation.SourceStateVersion != manifest.SourceStateVersion ||
            creation.SourceChecksum != manifest.SourceChecksum ||
            creation.CopiedScopeJson != manifest.CopiedScopeJson)
            throw new InvalidDataException("Manual draft creation audit disagrees with its manifest.");
        for (var version = 1; version < audit.Count; version++)
        {
            var entry = audit[version];
            if (entry.Action is not ("Edited" or "Excluded") ||
                entry.VersionAfter != version ||
                entry.TenantId != tenantId || entry.LegalEntityId != legalEntityId ||
                entry.RevisionId != revisionId)
                throw new InvalidDataException("Manual draft edit audit sequence is incomplete.");
        }
        var previousContentVersion = 0;
        for (var stateVersion = 0; stateVersion < reviewAudit.Count; stateVersion++)
        {
            var entry = reviewAudit[stateVersion];
            var expectedFingerprint = Hash(JsonSerializer.Serialize(new
            {
                actorId = entry.ActorId, action = entry.Action,
                reason = entry.Reason, expectedContentVersion = entry.ContentVersion,
                expectedStateVersion = stateVersion
            }, JsonOptions));
            if (entry.TenantId != tenantId || entry.LegalEntityId != legalEntityId ||
                entry.RevisionId != revisionId || entry.StateVersionAfter != stateVersion + 1 ||
                entry.ContentVersion < previousContentVersion ||
                entry.ContentVersion > manifest.Version || entry.ActorId == Guid.Empty ||
                entry.OccurredAt == default || entry.CreatedAt != entry.OccurredAt ||
                string.IsNullOrWhiteSpace(entry.RequestKey) ||
                entry.Fingerprint != expectedFingerprint)
                throw new InvalidDataException("Manual draft review audit sequence is invalid.");
            previousContentVersion = entry.ContentVersion;
        }
        var cycle = JsonSerializer.Deserialize<PlanningCycle>(manifest.CycleJson, JsonOptions)
            ?? throw new InvalidDataException("Manual draft cycle snapshot is unavailable.");
        if (cycle.Id != manifest.PlanningCycleId || cycle.TenantId != tenantId ||
            cycle.LegalEntityId != legalEntityId)
            throw new InvalidDataException("Manual draft cycle scope is inconsistent.");
        DemandRevisionDraft draft;
        if (manifest.SourceRevisionId.HasValue)
            draft = RestoreRollbackDraft(manifest, cycle, parts, tenantId,
                legalEntityId, revisionId);
        else
        {
            if (manifest.SourceState is not null || manifest.SourceChecksum is not null ||
                manifest.CopiedScopeJson is not null || manifest.SourceStateVersion is not null ||
                parts.Any(x => x.SourceSeriesJson is not null))
                throw new InvalidDataException("Legacy draft has unexpected rollback lineage.");
            var series = parts.OrderBy(x => x.SkuId).ThenBy(x => x.WarehouseId)
                .Select(x => ReadInitialSeries(x, tenantId, legalEntityId, revisionId))
                .ToArray();
            var created = ManualDraftFactory.Create(cycle, tenantId, legalEntityId,
                manifest.CreatedBy, manifest.CreationReason, manifest.CreatedAt, series);
            if (!created.IsSuccessful || created.Data is null)
                throw new InvalidDataException("Manual draft source snapshot failed validation.");
            draft = created.Data;
            draft.Id = revisionId;
        }
        var nextReview = 0;
        for (var contentVersion = 0; contentVersion <= manifest.Version; contentVersion++)
        {
            if (contentVersion > 0)
            {
                var entry = audit[contentVersion];
                if (entry.Action == "Edited")
                {
                    var change = JsonSerializer.Deserialize<DraftWeekChange>(entry.ChangeJson
                        ?? throw new InvalidDataException("Manual draft audit change is missing."), JsonOptions);
                    if (change is null || entry.VersionAfter != draft.Version + 1 ||
                        change.ActorId != entry.ActorId || change.RequestKey != entry.RequestKey ||
                        change.Reason != entry.Reason || change.OccurredAt != entry.OccurredAt)
                        throw new InvalidDataException("Manual draft audit sequence is inconsistent.");
                    var old = draft.Series.SingleOrDefault(x => x.SkuId == change.SkuId &&
                        x.WarehouseId == change.WarehouseId)?.Weeks.SingleOrDefault(x =>
                        x.Number == change.WeekNumber);
                    if (old?.ValueKind != change.OldKind || old?.Quantity != change.OldQuantity ||
                        draft.EditManualWeek(tenantId, legalEntityId, entry.ActorId,
                            change.SkuId, change.WarehouseId, change.WeekNumber,
                            change.NewKind, change.NewQuantity, entry.Reason, entry.RequestKey,
                            draft.Version, entry.OccurredAt) != DraftEditOutcome.Changed)
                        throw new InvalidDataException("Manual draft audit cannot be replayed safely.");
                }
                else
                {
                    var exclusion = JsonSerializer.Deserialize<DraftSeriesExclusion>(entry.ChangeJson
                        ?? throw new InvalidDataException("Manual draft exclusion audit is missing."), JsonOptions);
                    var expectedStateVersion = draft.StateVersion -
                        (exclusion?.PreviousState == DemandRevisionState.InReview ? 1 : 0);
                    var expectedFingerprint = exclusion is null ? string.Empty :
                        Hash(JsonSerializer.Serialize(new
                        {
                            actorId = exclusion.ActorId, skuId = exclusion.SkuId,
                            warehouseId = exclusion.WarehouseId,
                            reason = exclusion.Reason,
                            expectedContentVersion = draft.Version,
                            expectedStateVersion
                        }, JsonOptions));
                    if (exclusion is null || entry.VersionAfter != draft.Version + 1 ||
                        exclusion.ActorId != entry.ActorId || exclusion.RequestKey != entry.RequestKey ||
                        exclusion.Reason != entry.Reason || exclusion.OccurredAt != entry.OccurredAt ||
                        entry.Fingerprint != expectedFingerprint || expectedStateVersion < 0 ||
                        exclusion.NewState != DemandRevisionState.Draft ||
                        exclusion.PreviousState is not (DemandRevisionState.Draft or
                            DemandRevisionState.InReview) ||
                        (exclusion.PreviousState == DemandRevisionState.InReview &&
                            !reviewAudit.Any(x => x.Action == DraftReviewAction.Reopened &&
                                x.ContentVersion == draft.Version && x.ActorId == entry.ActorId &&
                                x.RequestKey == entry.RequestKey)) ||
                        draft.ExcludeSeries(tenantId, legalEntityId, entry.ActorId,
                            exclusion.SkuId, exclusion.WarehouseId, entry.Reason,
                            entry.RequestKey, draft.Version, entry.OccurredAt,
                            exclusion.PreviousState) != DraftEditOutcome.Changed)
                        throw new InvalidDataException("Manual draft exclusion audit cannot be replayed safely.");
                }
            }
            while (nextReview < reviewAudit.Count &&
                   reviewAudit[nextReview].ContentVersion == contentVersion)
            {
                var entry = reviewAudit[nextReview++];
                if (draft.ReplayPersistedReviewTransition(entry.Action, entry.ActorId,
                        entry.Reason, contentVersion, draft.StateVersion,
                        entry.OccurredAt) != DraftReviewOutcome.Changed)
                    throw new InvalidDataException("Manual draft review audit cannot be replayed safely.");
            }
        }
        for (var index = 0; index < publicationAudit.Count; index++)
        {
            var entry = publicationAudit[index];
            var expectedState = index == 0 ? DemandRevisionState.Published :
                index == 1 && entry.NewState == DemandRevisionState.Superseded
                    ? DemandRevisionState.Superseded : DemandRevisionState.Invalidated;
            var expectedFingerprint = expectedState == DemandRevisionState.Published
                ? Hash(JsonSerializer.Serialize(new
                {
                    tenantId, legalEntityId, revisionId, actorId = entry.ActorId,
                    key = entry.RequestKey,
                    expectedContentVersion = entry.ContentVersion,
                    expectedStateVersion = entry.StateVersionAfter - 1
                }, JsonOptions))
                : expectedState == DemandRevisionState.Superseded &&
                  entry.ReplacedByRevisionId is Guid replacement
                    ? Hash($"{tenantId:D}|{legalEntityId:D}|{revisionId:D}|{replacement:D}")
                : expectedState == DemandRevisionState.Invalidated &&
                  Enum.TryParse<InvalidationImpactCode>(entry.ImpactCode, out var impactCode) &&
                  Enum.IsDefined(impactCode) &&
                  impactCode != InvalidationImpactCode.ForecastDeviation &&
                  impactCode.ToString() == entry.ImpactCode &&
                  !string.IsNullOrWhiteSpace(entry.Reason) &&
                  !string.IsNullOrWhiteSpace(entry.EvidenceReference) &&
                  entry.ReplacedByRevisionId is null
                    ? InvalidationFingerprint.Calculate(tenantId, legalEntityId,
                        revisionId, entry.ActorId, entry.RequestKey, entry.Reason!,
                        impactCode, entry.EvidenceReference!, entry.ContentVersion,
                        entry.StateVersionAfter - 1)
                    : string.Empty;
            if (index > 2 || (index == 2 &&
                    publicationAudit[1].NewState != DemandRevisionState.Superseded) ||
                entry.TenantId != tenantId ||
                entry.LegalEntityId != legalEntityId || entry.RevisionId != revisionId ||
                entry.NewState != expectedState || entry.ActorId == Guid.Empty ||
                entry.ContentVersion != draft.Version ||
                entry.StateVersionAfter != draft.StateVersion + 1 ||
                string.IsNullOrWhiteSpace(entry.RequestKey) ||
                entry.Fingerprint != expectedFingerprint ||
                (expectedState == DemandRevisionState.Published &&
                    (entry.ReplacedByRevisionId is not null ||
                     draft.HasSignificantContribution(entry.ActorId))) ||
                entry.OccurredAt == default || entry.CreatedAt != entry.OccurredAt ||
                !draft.ApplyPublicationTransition(entry.NewState, entry.ActorId,
                    draft.Version, draft.StateVersion, enforcePublisherSeparation: false))
                throw new InvalidDataException("Manual draft publication audit cannot be replayed safely.");
        }
        if (draft.Version != manifest.Version || draft.UpdatedBy != manifest.UpdatedBy ||
            draft.UpdatedAt != manifest.UpdatedAt || draft.State != manifest.State ||
            draft.StateVersion != manifest.StateVersion ||
            draft.ReviewedBy != manifest.ReviewedBy ||
            draft.ReviewedAt != manifest.ReviewedAt ||
            draft.ReviewReason != manifest.ReviewReason)
            throw new InvalidDataException("Manual draft manifest and audit disagree.");
        var slotCount = await context.ManualDraftReviewSlots.CountDocumentsAsync(session,
            x => x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                 x.PlanningCycleId == manifest.PlanningCycleId &&
                 x.RevisionId == revisionId && !x.IsDeleted,
            cancellationToken: cancellationToken);
        if (slotCount != (draft.State is DemandRevisionState.InReview or
                DemandRevisionState.Approved ? 1 : 0))
            throw new InvalidDataException("Manual draft candidate slot disagrees with state.");
        return (manifest, draft);
    }

    private static VerifiedDraftSeries ReadInitialSeries(ManualDraftSeriesPart part,
        Guid tenantId, Guid legalEntityId, Guid revisionId)
    {
        if (part.TenantId != tenantId || part.LegalEntityId != legalEntityId ||
            part.RevisionId != revisionId || part.SkuId == Guid.Empty ||
            string.IsNullOrWhiteSpace(part.WarehouseId) ||
            string.IsNullOrWhiteSpace(part.InitialSeriesJson))
            throw new InvalidDataException("Manual draft series part identity is invalid.");

        VerifiedDraftSeries? series;
        try
        {
            series = JsonSerializer.Deserialize<VerifiedDraftSeries>(
                part.InitialSeriesJson, JsonOptions);
        }
        catch (Exception error) when (error is JsonException or NotSupportedException)
        {
            throw new InvalidDataException("Manual draft series part cannot be decoded.", error);
        }
        if (series is null || series.SkuId != part.SkuId ||
            !string.Equals(series.WarehouseId, part.WarehouseId, StringComparison.Ordinal))
            throw new InvalidDataException("Manual draft series part identity differs from its content.");
        return series;
    }

    private static PlanningCycle CycleFromDraft(DemandRevisionDraft draft) => new()
    {
        Id = draft.PlanningCycleId, TenantId = draft.TenantId,
        CreatedAt = default, // Volatile persistence time is not business request content.
        LegalEntityId = draft.LegalEntityId, AsOfDate = draft.AsOfDate,
        CalendarId = draft.CalendarId, CalendarVersion = draft.CalendarVersion,
        TimeZoneId = draft.TimeZoneId, HorizonStart = draft.HorizonStart,
        HorizonEnd = draft.HorizonEnd, PlanningPeriodKey = draft.PlanningPeriodKey,
        Weeks = draft.Weeks.ToList()
    };

    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void EnsureDocumentFits<T>(T value) where T : EntityBase
    {
        if (value.ToBson().Length > SafeDocumentBytes)
            throw new InvalidDataException("A manual draft physical part exceeds the safe BSON size.");
    }

    private static bool IsWriteConflict(MongoException error) =>
        error.HasErrorLabel("TransientTransactionError") ||
        error is MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey };

    private static async Task AbortIfActiveAsync(
        IClientSessionHandle session, CancellationToken cancellationToken)
    {
        if (session.IsInTransaction)
            await session.AbortTransactionAsync(cancellationToken);
    }
}
