using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

// LSKU FIX2 item 1: tenant-free on purpose (see IChildCreationAdmissionCensus) — it returns a count and nothing else.
public sealed class ChildCreationAdmissionCensusRepository(IMongoDatabase database) : IChildCreationAdmissionCensus
{
    private readonly IMongoCollection<Gsku> _gskus = database.GetCollection<Gsku>("mdm_gskus");

    public async Task<long> CountStaleAsync(DateTimeOffset acquiredBeforeUtc, CancellationToken cancellationToken = default)
    {
        // Not Eq(IsDeleted, false): a document written before the field existed has no IsDeleted (FIX2 item 13).
        var holders = await _gskus.Find(Builders<Gsku>.Filter.Ne(x => x.IsDeleted, true)
                & Builders<Gsku>.Filter.ElemMatch(x => x.ChildCreationAdmissions, x => x.AcquiredAtUtc < acquiredBeforeUtc))
            .Project(x => x.ChildCreationAdmissions).ToListAsync(cancellationToken);
        return holders.Sum(list => (long)list.Count(x => x.AcquiredAtUtc < acquiredBeforeUtc));
    }
}
