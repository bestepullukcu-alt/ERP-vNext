using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

// A source read, new manifest/parts and mandatory creation audit share one Mongo transaction.
public sealed class RollbackDraftMongoStore(DemandPlanningMongoContext context,
    PublishedRevisionMongoStore published, ManualDraftMongoStore drafts,
    IInternalSnapshotReadAuthority sourceAuthority,
    IManualDraftAuthority draftAuthority) : IRollbackDraftStore
{
    public async Task<RollbackDraftResult> CreateAsync(Guid tenantId, Guid legalEntityId,
        Guid sourceRevisionId, Guid actorId, string reason, string requestKey,
        int expectedSourceStateVersion, DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty ||
            sourceRevisionId == Guid.Empty || actorId == Guid.Empty ||
            string.IsNullOrWhiteSpace(reason) || reason.Length > 2_000 ||
            string.IsNullOrWhiteSpace(requestKey) || requestKey.Length > 200 ||
            expectedSourceStateVersion < 1 || occurredAt == default)
            return new(RollbackDraftOutcome.InvalidSource);
        SnapshotReadAuthorityEvidence evidence;
        try
        {
            evidence = await sourceAuthority.VerifyAsync(tenantId, legalEntityId,
                sourceRevisionId, actorId, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new(RollbackDraftOutcome.AuthorityUnavailable);
        }
        if (!evidence.SourceAvailable) return new(RollbackDraftOutcome.AuthorityUnavailable);
        if (!evidence.HasReadPermission) return new(RollbackDraftOutcome.PermissionDenied);
        if (!evidence.RevisionScopeVerified || evidence.TenantId != tenantId ||
            evidence.LegalEntityId != legalEntityId || evidence.RevisionId != sourceRevisionId ||
            evidence.AuthorizedSeries is null || evidence.AuthorizedSeries.Count == 0 ||
            evidence.AuthorizedSeries.Distinct().Count() != evidence.AuthorizedSeries.Count)
            return new(RollbackDraftOutcome.NotFound);

        await drafts.EnsureIndexesAsync(cancellationToken);
        using var session = await context.StartSessionAsync(cancellationToken);
        session.StartTransaction(new TransactionOptions(readConcern: ReadConcern.Snapshot));
        try
        {
            var source = await published.ReadSnapshotAsync(session, tenantId,
                legalEntityId, sourceRevisionId, cancellationToken);
            if (source is null)
                return await Abort(session, RollbackDraftOutcome.NotFound, cancellationToken);
            var manifest = source.Value.Manifest;
            if (manifest.State == DemandRevisionState.Invalidated)
                return await Abort(session, RollbackDraftOutcome.InvalidSource, cancellationToken);
            if (manifest.State is not (DemandRevisionState.Published or
                DemandRevisionState.Superseded))
                return await Abort(session, RollbackDraftOutcome.InvalidSource, cancellationToken);
            var keys = manifest.SelectedSeries.Select(x => new DraftSeriesKey(
                x.SkuId, x.WarehouseId)).Concat(manifest.ExcludedSeries.Select(x =>
                    new DraftSeriesKey(x.SkuId, x.WarehouseId))).ToArray();
            if (keys.Length == 0 || keys.Distinct().Count() != keys.Length ||
                keys.Any(x => !evidence.AuthorizedSeries.Contains((x.SkuId, x.WarehouseId))))
                return await Abort(session, RollbackDraftOutcome.NotFound, cancellationToken);
            bool? access;
            try
            {
                access = await draftAuthority.CanAccessAsync(tenantId, actorId,
                    legalEntityId, keys, cancellationToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                return await Abort(session, RollbackDraftOutcome.AuthorityUnavailable,
                    cancellationToken);
            }
            if (access is null)
                return await Abort(session, RollbackDraftOutcome.AuthorityUnavailable,
                    cancellationToken);
            if (!access.Value)
                return await Abort(session, RollbackDraftOutcome.NotFound, cancellationToken);

            var key = requestKey.Trim();
            var normalizedReason = reason.Trim();
            var existing = await context.ManualDraftManifests.Find(session, x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.PlanningCycleId == manifest.PlanningCycleId && x.CreateRequestKey == key)
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return await ReplayOrConflict(existing, manifest, actorId,
                    normalizedReason, expectedSourceStateVersion, cancellationToken);
            }
            if (manifest.StateVersion != expectedSourceStateVersion)
                return await Abort(session, RollbackDraftOutcome.Conflict, cancellationToken);
            // This write conflicts with publish/supersede/invalidate replacements of the
            // same manifest, while leaving business state, content and audit untouched.
            var sourceFilter = Builders<PublishedRevisionManifest>.Filter.Where(x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.RevisionId == sourceRevisionId && x.State == manifest.State &&
                x.StateVersion == expectedSourceStateVersion && !x.IsDeleted);
            var fenced = await context.PublishedRevisionManifests.UpdateOneAsync(session,
                sourceFilter,
                Builders<PublishedRevisionManifest>.Update.Inc(x =>
                    x.RollbackFenceVersion, 1), cancellationToken: cancellationToken);
            if (fenced.ModifiedCount != 1)
                return await Abort(session, RollbackDraftOutcome.Conflict, cancellationToken);
            var created = await drafts.CreateFromPublishedAsync(session, manifest,
                source.Value.Parts, actorId, normalizedReason, key, occurredAt,
                cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            return new(RollbackDraftOutcome.Created, created);
        }
        catch (MongoException error) when (error.HasErrorLabel("TransientTransactionError") ||
            error is MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey })
        {
            await AbortIfActive(session, cancellationToken);
            ManualDraftManifest? existing = null;
            for (var attempt = 0; attempt < 5 && existing is null; attempt++)
            {
                if (attempt > 0) await Task.Delay(50, cancellationToken);
                existing = await context.ManualDraftManifests.Find(x =>
                    x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                    x.CreateRequestKey == requestKey.Trim()).FirstOrDefaultAsync(cancellationToken);
            }
            var source = await published.ReadSnapshotAsync(tenantId,
                legalEntityId, sourceRevisionId, cancellationToken);
            if (source?.Manifest.State == DemandRevisionState.Invalidated)
                return new(RollbackDraftOutcome.InvalidSource);
            if (existing is null)
                return source?.Manifest.StateVersion != expectedSourceStateVersion
                    ? new(RollbackDraftOutcome.Conflict)
                    : new(RollbackDraftOutcome.AuthorityUnavailable);
            return source is null ? new(RollbackDraftOutcome.Conflict) :
                await ReplayOrConflict(existing, source.Value.Manifest, actorId,
                    reason.Trim(), expectedSourceStateVersion, cancellationToken);
        }
        catch (InvalidDataException)
        {
            await AbortIfActive(session, cancellationToken);
            return new(RollbackDraftOutcome.InvalidSnapshot);
        }
        catch
        {
            await AbortIfActive(session, cancellationToken);
            throw;
        }
    }

    private async Task<RollbackDraftResult> ReplayOrConflict(
        ManualDraftManifest existing, PublishedRevisionManifest source,
        Guid actorId, string reason, int requestedSourceStateVersion,
        CancellationToken cancellationToken)
    {
        if (existing.SourceRevisionId != source.RevisionId ||
            existing.SourceStateVersion != requestedSourceStateVersion ||
            existing.SourceChecksum != source.Checksum ||
            existing.CreatedBy != actorId || existing.CreationReason != reason)
            return new(RollbackDraftOutcome.Conflict);
        var draft = await drafts.ReadAsync(existing.TenantId, existing.LegalEntityId,
            existing.Id, cancellationToken);
        return draft is null ? new(RollbackDraftOutcome.Conflict) :
            new(RollbackDraftOutcome.Replayed, draft);
    }

    private static async Task<RollbackDraftResult> Abort(IClientSessionHandle session,
        RollbackDraftOutcome outcome, CancellationToken cancellationToken)
    {
        await session.AbortTransactionAsync(cancellationToken);
        return new(outcome);
    }

    private static async Task AbortIfActive(IClientSessionHandle session,
        CancellationToken cancellationToken)
    {
        if (session.IsInTransaction)
            await session.AbortTransactionAsync(cancellationToken);
    }
}
