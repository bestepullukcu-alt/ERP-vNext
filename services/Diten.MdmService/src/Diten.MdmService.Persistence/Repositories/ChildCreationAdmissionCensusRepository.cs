using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

// LSKU FIX2 item 1: tenant-free on purpose (see IChildCreationAdmissionCensus) — it returns a count and nothing else.
public sealed class ChildCreationAdmissionCensusRepository(IMongoDatabase database) : IChildCreationAdmissionCensus
{
    private readonly IMongoCollection<Gsku> _gskus = database.GetCollection<Gsku>("mdm_gskus");

    public async Task<long> CountStaleAsync(DateTimeOffset acquiredBeforeUtc, CancellationToken cancellationToken = default)
    {
        // Not Eq(IsDeleted, false): a document written before the field existed has no IsDeleted (FIX2 item 13).
        // FIX3 M5: counted by the server ($unwind + $match + $count), not every holder's list in memory; the first
        // $match uses the partial index on ChildCreationAdmissions.AcquiredAtUtc (GskuRepository.EnsureIndexes).
        var counted = await _gskus.Aggregate()
            .Match(Builders<Gsku>.Filter.Ne(x => x.IsDeleted, true)
                   & Builders<Gsku>.Filter.ElemMatch(x => x.ChildCreationAdmissions, x => x.AcquiredAtUtc < acquiredBeforeUtc))
            .Unwind<Gsku, UnwoundAdmission>(x => x.ChildCreationAdmissions)
            .Match(Builders<UnwoundAdmission>.Filter.Lt(x => x.ChildCreationAdmissions.AcquiredAtUtc, acquiredBeforeUtc))
            .Count()
            .FirstOrDefaultAsync(cancellationToken);
        return counted?.Count ?? 0;
    }

    [MongoDB.Bson.Serialization.Attributes.BsonIgnoreExtraElements]
    private sealed class UnwoundAdmission
    {
        public GskuChildCreationAdmission ChildCreationAdmissions { get; set; } = new();
    }

    // FG FIX1 item 1: tenant-free like the count above; the disposition is the same proof the GSKU blocker uses.
    public async Task<long> CountPendingFinishedGoodCancellationsAsync(DateTimeOffset requestedBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        // FG FIX1 review m3: only the fields the proof reads (the partial index on DraftCancellation.RequestedAtUtc,
        // FinishedGoodRepository.EnsureIndexes, serves the filter) — not every cancelled FG's whole document.
        var candidates = await database.GetCollection<FinishedGood>("mdm_finished_goods")
            .Find(Builders<FinishedGood>.Filter.Ne(x => x.IsDeleted, true)
                  & Builders<FinishedGood>.Filter.Ne(x => x.DraftCancellation, null)
                  & Builders<FinishedGood>.Filter.Lt(x => x.DraftCancellation!.RequestedAtUtc, requestedBeforeUtc))
            .Project<FinishedGood>(Builders<FinishedGood>.Projection
                .Include(x => x.Id).Include(x => x.TenantId).Include(x => x.CanonicalCode)
                .Include(x => x.CreationCommandId).Include(x => x.DraftCancellation)
                .Include("AuditIntents.IntentId").Include(x => x.AuditIntentReceipts))
            .ToListAsync(cancellationToken);
        return candidates.LongCount(fg => Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle
            .FinishedGoodDraftCancellationProof.Disposition(fg) != Diten.MdmService.Domain.ValueObjects
            .FinishedGoodCancellationDisposition.Cancelled);
    }
}
