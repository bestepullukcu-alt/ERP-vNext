using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

// Reads only lifecycle metadata and durable transition evidence. A corrupt
// physical part must not conceal an already committed Invalidated state.
public sealed class AuthoritativeRevisionStatusReader(
    DemandPlanningMongoContext context, IInternalRevisionStatusAuthority authority)
    : IInternalAuthoritativeRevisionStatusReader
{
    public async Task<AuthoritativeStatusResult> ReadAsync(Guid tenantId,
        Guid legalEntityId, Guid revisionId, Guid actorId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty ||
            revisionId == Guid.Empty || actorId == Guid.Empty)
            return new(AuthoritativeStatusOutcome.NotFound);

        RevisionStatusAuthorityEvidence evidence;
        try
        {
            evidence = await authority.VerifyAsync(tenantId, legalEntityId,
                revisionId, actorId, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new(AuthoritativeStatusOutcome.AuthorityUnavailable);
        }
        if (!evidence.SourceAvailable)
            return new(AuthoritativeStatusOutcome.AuthorityUnavailable);
        if (!evidence.HasConsumePermission)
            return new(AuthoritativeStatusOutcome.PermissionDenied);
        if (!evidence.RevisionScopeVerified || evidence.TenantId != tenantId ||
            evidence.LegalEntityId != legalEntityId || evidence.RevisionId != revisionId ||
            evidence.AuthorizedSeries is null || evidence.AuthorizedSeries.Count == 0 ||
            evidence.AuthorizedSeries.Any(x => x.SkuId == Guid.Empty ||
                string.IsNullOrWhiteSpace(x.WarehouseId)) ||
            evidence.AuthorizedSeries.Distinct().Count() != evidence.AuthorizedSeries.Count)
            return new(AuthoritativeStatusOutcome.NotFound);

        try
        {
            using var session = await context.StartSessionAsync(cancellationToken);
            session.StartTransaction(new TransactionOptions(readConcern: ReadConcern.Snapshot));
            try
            {
                var result = await ReadAuthorizedAsync(session, tenantId,
                    legalEntityId, revisionId, evidence, cancellationToken);
                await session.CommitTransactionAsync(cancellationToken);
                return result;
            }
            catch
            {
                if (session.IsInTransaction)
                    await session.AbortTransactionAsync(cancellationToken);
                throw;
            }
        }
        catch (MongoException)
        {
            return new(AuthoritativeStatusOutcome.StoreUnavailable);
        }
        catch (InvalidDataException)
        {
            return new(AuthoritativeStatusOutcome.Inconsistent);
        }
    }

    private async Task<AuthoritativeStatusResult> ReadAuthorizedAsync(
        IClientSessionHandle session, Guid tenantId, Guid legalEntityId,
        Guid revisionId, RevisionStatusAuthorityEvidence evidence,
        CancellationToken cancellationToken)
    {
        var published = await context.PublishedRevisionManifests.Find(session, x =>
            x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
            x.RevisionId == revisionId && !x.IsDeleted)
            .Limit(2).ToListAsync(cancellationToken);
        if (published.Count == 0)
            return new(AuthoritativeStatusOutcome.NotFound);
        if (published.Count != 1)
            throw new InvalidDataException("Duplicate published revision identity.");
        var manifest = published[0];
        if (manifest.SelectedSeries is null || manifest.SelectedSeries.Count == 0 ||
            manifest.SelectedSeries.Any(x => x.SkuId == Guid.Empty ||
                string.IsNullOrWhiteSpace(x.WarehouseId)) ||
            manifest.SelectedSeries.Select(x => (x.SkuId, x.WarehouseId))
                .Distinct().Count() != manifest.SelectedSeries.Count ||
            manifest.SelectedSeries.Count != evidence.AuthorizedSeries.Count ||
            !manifest.SelectedSeries.Select(x => (x.SkuId, x.WarehouseId))
                .ToHashSet().SetEquals(evidence.AuthorizedSeries))
            return new(AuthoritativeStatusOutcome.NotFound);

        var manuals = await context.ManualDraftManifests.Find(session, x =>
            x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
            x.Id == revisionId && !x.IsDeleted).Limit(2)
            .ToListAsync(cancellationToken);
        if (manuals.Count != 1)
            throw new InvalidDataException("Revision manifest is absent or duplicated.");
        var manual = manuals[0];
        var draftAudit = await context.ManualDraftAudit.Find(session, x =>
            x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
            x.RevisionId == revisionId && !x.IsDeleted)
            .SortBy(x => x.VersionAfter).ToListAsync(cancellationToken);
        var reviews = await context.ManualDraftReviewAudit.Find(session, x =>
            x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
            x.RevisionId == revisionId && !x.IsDeleted)
            .SortBy(x => x.StateVersionAfter).ToListAsync(cancellationToken);
        var transitions = await context.ManualDraftPublicationAudit.Find(session, x =>
            x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
            x.RevisionId == revisionId && !x.IsDeleted)
            .SortBy(x => x.StateVersionAfter).ToListAsync(cancellationToken);
        var slots = await context.PublishedBaselineSlots.Find(session, x =>
            x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
            x.PlanningPeriodKey == manifest.PlanningPeriodKey && !x.IsDeleted)
            .Limit(2).ToListAsync(cancellationToken);
        if (manual.State != manifest.State ||
            manual.StateVersion != manifest.StateVersion ||
            manual.Version != manifest.ContentVersion ||
            manual.PlanningCycleId != manifest.PlanningCycleId ||
            string.IsNullOrWhiteSpace(manifest.PlanningPeriodKey) ||
            manifest.PlanningPeriodKey != manifest.HorizonStart.ToString("yyyy-MM-dd") ||
            manifest.PublishedBy == Guid.Empty || manifest.PublishedAt == default ||
            (manifest.PreparedAt != default &&
                manifest.PreparedAt != manual.CreatedAt) ||
            manifest.CreatedBy != manual.CreatedBy ||
            manifest.State is not (DemandRevisionState.Published or
                DemandRevisionState.Superseded or DemandRevisionState.Invalidated) ||
            manifest.StateVersion < 1 || manifest.ContentVersion < 0 ||
            reviews.Count < 2 || reviews.Count + transitions.Count != manifest.StateVersion ||
            slots.Count != 1)
            throw new InvalidDataException("Revision status metadata does not reconcile.");

        ValidateReviews(reviews, tenantId, legalEntityId, revisionId,
            manifest.ContentVersion, manual, manifest);
        ValidateCreationAudit(draftAudit, tenantId, legalEntityId,
            revisionId, manual);
        ValidateTransitions(transitions, reviews.Count, tenantId,
            legalEntityId, revisionId, manifest);
        var slot = slots[0];
        if ((manifest.State == DemandRevisionState.Published &&
                (!slot.IsAvailable || slot.RevisionId != revisionId)) ||
            (manifest.State == DemandRevisionState.Superseded &&
                slot.IsAvailable && slot.RevisionId == revisionId) ||
            (manifest.State == DemandRevisionState.Invalidated &&
                slot.RevisionId == revisionId && slot.IsAvailable))
            throw new InvalidDataException("Current baseline slot contradicts revision state.");

        return new(AuthoritativeStatusOutcome.Found,
            new AuthoritativeRevisionStatus(tenantId, legalEntityId, revisionId,
                manifest.PlanningCycleId, manifest.PlanningPeriodKey,
                manifest.State, manifest.StateVersion, manifest.ContentVersion,
                manifest.SelectedSeries.Select(x => (x.SkuId, x.WarehouseId)).ToArray(),
                manifest.ExcludedSeries.Count,
                manifest.CreatedBy, manual.CreatedAt,
                manifest.SignificantEditorIds.ToArray(), manifest.ReviewedBy,
                manifest.ReviewedAt, manifest.PublishedBy, manifest.PublishedAt,
                manifest.State == DemandRevisionState.Invalidated
                    ? transitions.Last().Reason : null,
                manifest.State == DemandRevisionState.Invalidated
                    ? transitions.Last().ImpactCode : null,
                manifest.State == DemandRevisionState.Invalidated
                    ? transitions.Last().EvidenceReference : null,
                manifest.State == DemandRevisionState.Invalidated
                    ? transitions.Last().ActorId : null,
                manifest.State == DemandRevisionState.Invalidated
                    ? transitions.Last().OccurredAt : null));
    }

    private static void ValidateCreationAudit(
        IReadOnlyList<ManualDraftAuditRecord> audit, Guid tenantId,
        Guid legalEntityId, Guid revisionId, ManualDraftManifest manual)
    {
        if (audit.Count != manual.Version + 1 || audit.Count == 0 ||
            audit.Count(x => x.Action == "Created") != 1 ||
            audit.Where((entry, index) => entry.VersionAfter != index).Any())
            throw new InvalidDataException("Draft creation audit is incomplete.");
        var creation = audit[0];
        if (creation.Action != "Created" ||
            string.IsNullOrWhiteSpace(manual.CreateRequestKey) ||
            string.IsNullOrWhiteSpace(manual.CreateFingerprint) ||
            string.IsNullOrWhiteSpace(manual.CreationReason) ||
            manual.CreatedAt == default || manual.CreatedBy == Guid.Empty ||
            creation.TenantId != tenantId ||
            creation.LegalEntityId != legalEntityId ||
            creation.RevisionId != revisionId ||
            creation.RequestKey != manual.CreateRequestKey ||
            creation.Fingerprint != manual.CreateFingerprint ||
            creation.ActorId != manual.CreatedBy ||
            creation.Reason != manual.CreationReason ||
            creation.OccurredAt != manual.CreatedAt ||
            creation.CreatedAt != manual.CreatedAt ||
            creation.ChangeJson is not null)
            throw new InvalidDataException("Draft preparation time is not proven.");
    }

    private static void ValidateReviews(IReadOnlyList<ManualDraftReviewAuditRecord> reviews,
        Guid tenantId, Guid legalEntityId, Guid revisionId, int contentVersion,
        ManualDraftManifest manual, PublishedRevisionManifest published)
    {
        var previousContentVersion = 0;
        for (var index = 0; index < reviews.Count; index++)
        {
            var entry = reviews[index];
            var expected = Hash(JsonSerializer.Serialize(new
            {
                actorId = entry.ActorId, action = entry.Action,
                reason = entry.Reason, expectedContentVersion = entry.ContentVersion,
                expectedStateVersion = index
            }));
            if (entry.TenantId != tenantId || entry.LegalEntityId != legalEntityId ||
                entry.RevisionId != revisionId || entry.ActorId == Guid.Empty ||
                entry.StateVersionAfter != index + 1 ||
                entry.ContentVersion < previousContentVersion ||
                entry.ContentVersion > contentVersion ||
                string.IsNullOrWhiteSpace(entry.RequestKey) ||
                entry.OccurredAt == default || entry.CreatedAt != entry.OccurredAt ||
                entry.Fingerprint != expected)
                throw new InvalidDataException("Review status evidence is inconsistent.");
            previousContentVersion = entry.ContentVersion;
        }
        var last = reviews[^1];
        if (last.Action != DraftReviewAction.Approved ||
            last.ContentVersion != contentVersion ||
            manual.ReviewedBy != last.ActorId || published.ReviewedBy != last.ActorId ||
            manual.ReviewedAt != last.OccurredAt ||
            published.ReviewedAt != last.OccurredAt ||
            manual.ReviewReason != last.Reason || published.ReviewReason != last.Reason)
            throw new InvalidDataException("Final approval evidence is inconsistent.");
    }

    private static void ValidateTransitions(
        IReadOnlyList<ManualDraftPublicationAuditRecord> transitions,
        int reviewCount, Guid tenantId, Guid legalEntityId, Guid revisionId,
        PublishedRevisionManifest manifest)
    {
        var expectedCount = manifest.State switch
        {
            DemandRevisionState.Published => 1,
            DemandRevisionState.Superseded => 2,
            DemandRevisionState.Invalidated => transitions.Count is 2 or 3
                ? transitions.Count : -1,
            _ => -1
        };
        if (transitions.Count != expectedCount)
            throw new InvalidDataException("Publication status chain is incomplete.");
        for (var index = 0; index < transitions.Count; index++)
        {
            var entry = transitions[index];
            var priorState = index == 0 ? DemandRevisionState.Approved :
                transitions[index - 1].NewState;
            var allowed = (priorState, entry.NewState) switch
            {
                (DemandRevisionState.Approved, DemandRevisionState.Published) => true,
                (DemandRevisionState.Published, DemandRevisionState.Superseded) => true,
                (DemandRevisionState.Published, DemandRevisionState.Invalidated) => true,
                (DemandRevisionState.Superseded, DemandRevisionState.Invalidated) => true,
                _ => false
            };
            if (!allowed || entry.TenantId != tenantId ||
                entry.LegalEntityId != legalEntityId || entry.RevisionId != revisionId ||
                entry.ActorId == Guid.Empty ||
                entry.ContentVersion != manifest.ContentVersion ||
                entry.StateVersionAfter != reviewCount + index + 1 ||
                string.IsNullOrWhiteSpace(entry.RequestKey) ||
                entry.OccurredAt == default || entry.CreatedAt != entry.OccurredAt ||
                entry.Fingerprint != ExpectedFingerprint(entry, tenantId,
                    legalEntityId, revisionId))
                throw new InvalidDataException("Publication transition audit disagrees.");
        }
        var published = transitions[0];
        if (published.ActorId != manifest.PublishedBy ||
            published.OccurredAt != manifest.PublishedAt ||
            transitions[^1].NewState != manifest.State)
            throw new InvalidDataException("Durable status differs from transition audit.");
    }

    private static string ExpectedFingerprint(ManualDraftPublicationAuditRecord entry,
        Guid tenantId, Guid legalEntityId, Guid revisionId)
    {
        if (entry.NewState == DemandRevisionState.Published &&
            entry.ReplacedByRevisionId is null)
            return Hash(JsonSerializer.Serialize(new
            {
                tenantId, legalEntityId, revisionId, actorId = entry.ActorId,
                key = entry.RequestKey,
                expectedContentVersion = entry.ContentVersion,
                expectedStateVersion = entry.StateVersionAfter - 1
            }));
        if (entry.NewState == DemandRevisionState.Superseded &&
            entry.ReplacedByRevisionId is Guid replacement &&
            replacement != Guid.Empty)
            return Hash($"{tenantId:D}|{legalEntityId:D}|{revisionId:D}|{replacement:D}");
        if (entry.NewState == DemandRevisionState.Invalidated &&
            entry.ReplacedByRevisionId is null &&
            entry.RequestKey.StartsWith("invalidate:", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(entry.Reason) &&
            !string.IsNullOrWhiteSpace(entry.EvidenceReference) &&
            Enum.TryParse<InvalidationImpactCode>(entry.ImpactCode, out var impact) &&
            Enum.IsDefined(impact) &&
            impact != InvalidationImpactCode.ForecastDeviation &&
            impact.ToString() == entry.ImpactCode)
            return InvalidationFingerprint.Calculate(tenantId, legalEntityId,
                revisionId, entry.ActorId, entry.RequestKey, entry.Reason!,
                impact, entry.EvidenceReference!, entry.ContentVersion,
                entry.StateVersionAfter - 1);
        return string.Empty;
    }

    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
