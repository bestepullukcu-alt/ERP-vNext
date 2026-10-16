using System.Security.Cryptography;
using System.Text;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

// Internal lifecycle command. Authority and material-impact evidence are
// independently resolved before the transaction touches a revision.
public sealed class InvalidationMongoStore(DemandPlanningMongoContext context,
    ManualDraftMongoStore drafts, PublishedRevisionMongoStore published,
    IInternalInvalidationAuthority authority, TimeProvider clock)
    : IInternalRevisionInvalidator
{
    public async Task<InvalidationResult> InvalidateAsync(Guid tenantId,
        Guid legalEntityId, Guid revisionId, Guid actorId,
        InvalidationImpactCode impactCode, string reason,
        string evidenceReference, string requestKey,
        int expectedContentVersion, int expectedStateVersion,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty ||
            revisionId == Guid.Empty || actorId == Guid.Empty ||
            !Enum.IsDefined(impactCode) ||
            impactCode == InvalidationImpactCode.ForecastDeviation ||
            string.IsNullOrWhiteSpace(reason) ||
            string.IsNullOrWhiteSpace(evidenceReference) ||
            string.IsNullOrWhiteSpace(requestKey) ||
            expectedContentVersion < 0 || expectedStateVersion < 0)
            return new(InvalidationOutcome.InvalidRequest);
        var normalizedReason = reason.Trim();
        var reference = evidenceReference.Trim();
        var key = $"invalidate:{requestKey.Trim()}";
        var fingerprint = InvalidationFingerprint.Calculate(tenantId, legalEntityId,
            revisionId, actorId, key, normalizedReason, impactCode, reference,
            expectedContentVersion, expectedStateVersion);
        InvalidationAuthorityEvidence evidence;
        try
        {
            evidence = await authority.VerifyAsync(tenantId, legalEntityId,
                revisionId, actorId, impactCode, reference, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new(InvalidationOutcome.AuthorityUnavailable);
        }
        if (!evidence.SourceAvailable)
            return new(InvalidationOutcome.AuthorityUnavailable);
        if (!evidence.HasInvalidatePermission)
            return new(InvalidationOutcome.PermissionDenied);
        if (!evidence.RevisionScopeVerified || evidence.TenantId != tenantId ||
            evidence.LegalEntityId != legalEntityId || evidence.RevisionId != revisionId ||
            evidence.AuthorizedSeries is null || evidence.AuthorizedSeries.Count == 0 ||
            evidence.AuthorizedSeries.Distinct().Count() != evidence.AuthorizedSeries.Count)
            return new(InvalidationOutcome.NotFound);
        if (!evidence.MaterialImpactVerified ||
            string.IsNullOrWhiteSpace(evidence.VerifiedBusinessImpact) ||
            evidence.ImpactCode != impactCode ||
            !string.Equals(evidence.EvidenceReference, reference,
                StringComparison.Ordinal))
            return new(InvalidationOutcome.MaterialImpactMissing);
        var businessImpact = evidence.VerifiedBusinessImpact.Trim();

        await published.EnsureIndexesAsync(cancellationToken);
        using var session = await context.StartSessionAsync(cancellationToken);
        session.StartTransaction(new TransactionOptions(
            readConcern: ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority));
        try
        {
            var snapshot = await published.ReadSnapshotAsync(session, tenantId,
                legalEntityId, revisionId, cancellationToken);
            if (snapshot is null)
                return await Abort(session, InvalidationOutcome.NotFound,
                    cancellationToken);
            var (manifest, _) = snapshot.Value;
            if (manifest.SelectedSeries.Count != evidence.AuthorizedSeries.Count ||
                !manifest.SelectedSeries.Select(x => (x.SkuId, x.WarehouseId))
                    .ToHashSet().SetEquals(evidence.AuthorizedSeries))
                return await Abort(session, InvalidationOutcome.InvalidSnapshot,
                    cancellationToken);
            var loaded = await drafts.LoadAsync(session, tenantId, legalEntityId,
                revisionId, cancellationToken);
            if (loaded is null)
                throw new InvalidDataException("Revision Draft is absent.");
            var (manualManifest, draft) = loaded.Value;
            var prior = await context.ManualDraftPublicationAudit.Find(session, x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.RevisionId == revisionId && x.RequestKey == key && !x.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            if (prior is not null)
                return await Abort(session,
                    prior.NewState == DemandRevisionState.Invalidated &&
                    prior.Fingerprint == fingerprint
                        ? InvalidationOutcome.Replayed : InvalidationOutcome.Conflict,
                    cancellationToken, revisionId, prior.StateVersionAfter);
            if (manifest.State is not (DemandRevisionState.Published or
                    DemandRevisionState.Superseded) ||
                manifest.ContentVersion != expectedContentVersion ||
                manifest.StateVersion != expectedStateVersion ||
                draft.Version != expectedContentVersion ||
                draft.StateVersion != expectedStateVersion)
                return await Abort(session, InvalidationOutcome.Conflict,
                    cancellationToken);
            var oldState = manifest.State;
            var nextStateVersion = expectedStateVersion + 1;
            var occurredAt = clock.GetUtcNow();
            if (!draft.ApplyPublicationTransition(DemandRevisionState.Invalidated,
                    actorId, expectedContentVersion, expectedStateVersion,
                    enforcePublisherSeparation: false))
                return await Abort(session, InvalidationOutcome.Conflict,
                    cancellationToken);

            var slot = await context.PublishedBaselineSlots.Find(session, x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.PlanningPeriodKey == manifest.PlanningPeriodKey && !x.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            if (oldState == DemandRevisionState.Published &&
                (slot is null || !slot.IsAvailable || slot.RevisionId != revisionId))
                throw new InvalidDataException("Current baseline slot disagrees with Published state.");
            if (oldState == DemandRevisionState.Superseded &&
                slot?.IsAvailable == true && slot.RevisionId == revisionId)
                throw new InvalidDataException("Superseded revision occupies the current slot.");

            var manualFilter = Builders<ManualDraftManifest>.Filter.Where(x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.Id == revisionId && x.State == oldState &&
                x.Version == expectedContentVersion &&
                x.StateVersion == expectedStateVersion && !x.IsDeleted);
            manualManifest.State = DemandRevisionState.Invalidated;
            manualManifest.StateVersion = nextStateVersion;
            if ((await context.ManualDraftManifests.ReplaceOneAsync(session,
                    manualFilter, manualManifest,
                    cancellationToken: cancellationToken)).ModifiedCount != 1)
                return await Abort(session, InvalidationOutcome.Conflict,
                    cancellationToken);

            var publishedFilter = Builders<PublishedRevisionManifest>.Filter.Where(x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.RevisionId == revisionId && x.State == oldState &&
                x.ContentVersion == expectedContentVersion &&
                x.StateVersion == expectedStateVersion && !x.IsDeleted);
            manifest.State = DemandRevisionState.Invalidated;
            manifest.StateVersion = nextStateVersion;
            if ((await context.PublishedRevisionManifests.ReplaceOneAsync(session,
                    publishedFilter, manifest,
                    cancellationToken: cancellationToken)).ModifiedCount != 1)
                return await Abort(session, InvalidationOutcome.Conflict,
                    cancellationToken);

            if (oldState == DemandRevisionState.Published)
            {
                var oldSlotVersion = slot!.Version;
                var slotFilter = Builders<PublishedBaselineSlot>.Filter.Where(x =>
                    x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                    x.PlanningPeriodKey == manifest.PlanningPeriodKey &&
                    x.RevisionId == revisionId && x.Version == oldSlotVersion &&
                    x.IsAvailable && !x.IsDeleted);
                slot.IsAvailable = false;
                slot.Version++;
                slot.UpdatedAt = occurredAt;
                if ((await context.PublishedBaselineSlots.ReplaceOneAsync(session,
                        slotFilter, slot,
                        cancellationToken: cancellationToken)).ModifiedCount != 1)
                    return await Abort(session, InvalidationOutcome.Conflict,
                        cancellationToken);
            }

            await context.ManualDraftPublicationAudit.InsertOneAsync(session,
                new ManualDraftPublicationAuditRecord
                {
                    TenantId = tenantId, LegalEntityId = legalEntityId,
                    RevisionId = revisionId, RequestKey = key,
                    Fingerprint = fingerprint,
                    NewState = DemandRevisionState.Invalidated,
                    ContentVersion = expectedContentVersion,
                    StateVersionAfter = nextStateVersion,
                    ActorId = actorId, OccurredAt = occurredAt,
                    CreatedAt = occurredAt, Reason = normalizedReason,
                    ImpactCode = impactCode.ToString(),
                    EvidenceReference = reference,
                    BusinessImpact = businessImpact
                }, cancellationToken: cancellationToken);
            await context.AuditEntries.InsertOneAsync(session, new DemandAuditEntry
            {
                TenantId = tenantId, LegalEntityId = legalEntityId,
                ActorId = actorId, Action = "Invalidated",
                Reason = normalizedReason, EvidenceReference = reference,
                OccurredAt = occurredAt, CreatedAt = occurredAt
            }, cancellationToken: cancellationToken);
            var eventId = DeterministicEventId(tenantId, legalEntityId, revisionId);
            await context.Outbox.InsertOneAsync(session, new DemandOutboxMessage
            {
                TenantId = tenantId, LegalEntityId = legalEntityId,
                RevisionId = revisionId, EventId = eventId,
                EventType = "demand.revision.invalidated.v2",
                StateVersion = nextStateVersion,
                Payload = DemandRevisionOutboxPayload.Invalidated(manifest, eventId,
                    occurredAt, impactCode, normalizedReason, businessImpact,
                    reference, actorId),
                OccurredAt = occurredAt, CreatedAt = occurredAt
            }, cancellationToken: cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            return new(InvalidationOutcome.Invalidated, revisionId,
                nextStateVersion);
        }
        catch (MongoException error) when (IsConflict(error))
        {
            await AbortIfActive(session, cancellationToken);
            var prior = await context.ManualDraftPublicationAudit.Find(x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.RevisionId == revisionId && x.RequestKey == key && !x.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            return new(prior?.NewState == DemandRevisionState.Invalidated &&
                prior.Fingerprint == fingerprint
                    ? InvalidationOutcome.Replayed : InvalidationOutcome.Conflict,
                revisionId, prior?.StateVersionAfter);
        }
        catch (InvalidDataException)
        {
            await AbortIfActive(session, cancellationToken);
            return new(InvalidationOutcome.InvalidSnapshot);
        }
        catch
        {
            await AbortIfActive(session, cancellationToken);
            throw;
        }
    }

    public static Guid DeterministicEventId(Guid tenantId, Guid legalEntityId,
        Guid revisionId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"MOD0188-INVALIDATED|{tenantId:D}|{legalEntityId:D}|{revisionId:D}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static bool IsConflict(MongoException error) =>
        error.HasErrorLabel("TransientTransactionError") ||
        error is MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey };

    private static async Task<InvalidationResult> Abort(IClientSessionHandle session,
        InvalidationOutcome outcome, CancellationToken cancellationToken,
        Guid? revisionId = null, int? stateVersion = null)
    {
        await AbortIfActive(session, cancellationToken);
        return new(outcome, revisionId, stateVersion);
    }

    private static async Task AbortIfActive(IClientSessionHandle session,
        CancellationToken cancellationToken)
    {
        if (session.IsInTransaction)
            await session.AbortTransactionAsync(cancellationToken);
    }
}
