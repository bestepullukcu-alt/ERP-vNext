using System.Globalization;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

// The period authority runs before any slot or manifest lookup. A verified
// snapshot and the authoritative status must agree before an identity leaves
// this reader. A racing publication yields a closed error, never a fallback.
public sealed class CurrentPublishedRevisionReader(
    DemandPlanningMongoContext context, PublishedRevisionMongoStore published,
    IInternalCurrentPublishedAuthority authority,
    IInternalAuthoritativeRevisionStatusReader statuses)
    : IInternalCurrentPublishedReader
{
    public async Task<CurrentPublishedResult> ReadAsync(Guid tenantId,
        Guid legalEntityId, string planningPeriodKey, Guid actorId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty ||
            actorId == Guid.Empty || string.IsNullOrWhiteSpace(planningPeriodKey) ||
            planningPeriodKey.Length != 10 ||
            !DateOnly.TryParseExact(planningPeriodKey, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var period) ||
            period.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) != planningPeriodKey)
            return new(CurrentPublishedOutcome.NotFound);

        CurrentPublishedAuthorityEvidence evidence;
        try
        {
            evidence = await authority.VerifyAsync(tenantId, legalEntityId,
                planningPeriodKey, actorId, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new(CurrentPublishedOutcome.AuthorityUnavailable);
        }
        if (!evidence.SourceAvailable)
            return new(CurrentPublishedOutcome.AuthorityUnavailable);
        if (!evidence.HasConsumePermission)
            return new(CurrentPublishedOutcome.PermissionDenied);
        if (!evidence.PeriodScopeVerified || evidence.TenantId != tenantId ||
            evidence.LegalEntityId != legalEntityId ||
            evidence.PlanningPeriodKey != planningPeriodKey ||
            evidence.AuthorizedSeries is null ||
            evidence.AuthorizedSeries.Count == 0 ||
            evidence.AuthorizedSeries.Any(x => x.SkuId == Guid.Empty ||
                string.IsNullOrWhiteSpace(x.WarehouseId)) ||
            evidence.AuthorizedSeries.Distinct().Count() != evidence.AuthorizedSeries.Count)
            return new(CurrentPublishedOutcome.NotFound);

        PublishedBaselineSlot slot;
        PublishedRevisionManifest manifest;
        try
        {
            using var session = await context.StartSessionAsync(cancellationToken);
            session.StartTransaction(new TransactionOptions(readConcern: ReadConcern.Snapshot));
            try
            {
                var slots = await context.PublishedBaselineSlots.Find(session, x =>
                    x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                    x.PlanningPeriodKey == planningPeriodKey && !x.IsDeleted)
                    .Limit(2).ToListAsync(cancellationToken);
                if (slots.Count == 0)
                    return await FinishAsync(session, CurrentPublishedOutcome.NotFound,
                        cancellationToken);
                if (slots.Count != 1 || slots[0].RevisionId == Guid.Empty)
                    return await FinishAsync(session, CurrentPublishedOutcome.Unverifiable,
                        cancellationToken);
                slot = slots[0];

                // Compare independently authorized series before attempting
                // checksum validation, which could otherwise reveal an
                // out-of-scope revision by its failure mode.
                var manifests = await context.PublishedRevisionManifests.Find(session, x =>
                    x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                    x.RevisionId == slot.RevisionId && !x.IsDeleted)
                    .Limit(2).ToListAsync(cancellationToken);
                if (manifests.Count != 1)
                    return await FinishAsync(session, CurrentPublishedOutcome.Unverifiable,
                        cancellationToken);
                manifest = manifests[0];
                if (manifest.SelectedSeries is null ||
                    manifest.SelectedSeries.Count == 0 ||
                    manifest.SelectedSeries.Select(x => (x.SkuId, x.WarehouseId))
                        .Distinct().Count() != manifest.SelectedSeries.Count ||
                    !manifest.SelectedSeries.All(x => evidence.AuthorizedSeries
                        .Contains((x.SkuId, x.WarehouseId))))
                    return await FinishAsync(session, CurrentPublishedOutcome.NotFound,
                        cancellationToken);
                if (manifest.PlanningPeriodKey != planningPeriodKey ||
                    manifest.TenantId != tenantId ||
                    manifest.LegalEntityId != legalEntityId)
                    return await FinishAsync(session, CurrentPublishedOutcome.Unverifiable,
                        cancellationToken);

                if (slot.IsAvailable)
                {
                    if (manifest.State != DemandRevisionState.Published)
                        return await FinishAsync(session, CurrentPublishedOutcome.Unverifiable,
                            cancellationToken);
                    var snapshot = await published.ReadSnapshotAsync(session,
                        tenantId, legalEntityId, slot.RevisionId, cancellationToken);
                    if (snapshot is null ||
                        snapshot.Value.Manifest.StateVersion != manifest.StateVersion ||
                        snapshot.Value.Manifest.Checksum != manifest.Checksum)
                        return await FinishAsync(session, CurrentPublishedOutcome.Unverifiable,
                            cancellationToken);
                }
                await session.CommitTransactionAsync(cancellationToken);
            }
            catch
            {
                if (session.IsInTransaction)
                    await session.AbortTransactionAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new(CurrentPublishedOutcome.Unverifiable);
        }

        // This reader also reconciles the durable state and transition audit.
        // It is separately authorized against revision-bound series scope.
        var status = await statuses.ReadAsync(tenantId, legalEntityId,
            slot.RevisionId, actorId, cancellationToken);
        if (status.Outcome == AuthoritativeStatusOutcome.PermissionDenied)
            return new(CurrentPublishedOutcome.PermissionDenied);
        if (status.Outcome == AuthoritativeStatusOutcome.NotFound)
            return new(CurrentPublishedOutcome.NotFound);
        if (status.Outcome != AuthoritativeStatusOutcome.Found || status.Status is null)
            return new(CurrentPublishedOutcome.Unverifiable);
        if (status.Status.PlanningPeriodKey != planningPeriodKey ||
            status.Status.StateVersion != manifest.StateVersion ||
            status.Status.RevisionId != slot.RevisionId)
            return new(CurrentPublishedOutcome.Unverifiable);
        if (!slot.IsAvailable)
            return status.Status.State == DemandRevisionState.Invalidated
                ? new(CurrentPublishedOutcome.NotFound)
                : new(CurrentPublishedOutcome.Unverifiable);
        if (status.Status.State != DemandRevisionState.Published)
            return new(CurrentPublishedOutcome.Unverifiable);

        try
        {
            var current = await context.PublishedBaselineSlots.Find(x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.PlanningPeriodKey == planningPeriodKey && !x.IsDeleted)
                .Limit(2).ToListAsync(cancellationToken);
            if (current.Count != 1 || !current[0].IsAvailable ||
                current[0].RevisionId != slot.RevisionId ||
                current[0].Version != slot.Version)
                return new(CurrentPublishedOutcome.Unverifiable);
        }
        catch (MongoException)
        {
            return new(CurrentPublishedOutcome.Unverifiable);
        }
        return new(CurrentPublishedOutcome.Found,
            new CurrentPublishedRevision(tenantId, legalEntityId,
                slot.RevisionId, manifest.PlanningCycleId, planningPeriodKey,
                manifest.StateVersion, manifest.ChecksumScheme,
                manifest.Checksum, manifest.ExpectedPartCount,
                manifest.ExpectedRowCount));
    }

    private static async Task<CurrentPublishedResult> FinishAsync(
        IClientSessionHandle session, CurrentPublishedOutcome outcome,
        CancellationToken cancellationToken)
    {
        await session.AbortTransactionAsync(cancellationToken);
        return new(outcome);
    }
}
