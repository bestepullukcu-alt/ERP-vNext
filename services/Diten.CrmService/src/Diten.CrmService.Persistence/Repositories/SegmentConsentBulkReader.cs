using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0167 FU02 Phase-2 consent read: TWO queries for the whole candidate set (consents, preferences), never one per
/// candidate. The rows are handed to the MOD-0164 evaluation engine in memory, so the segment resolver reuses MOD-0164
/// decision logic verbatim and consent semantics cannot drift between the two modules.
/// <para>Nothing in MOD-0164 changes: this reads the same collections its own repositories read, and it writes nothing.
/// The per-subject <c>IConsentPreferenceEvaluator</c> is still the right seam for the single-subject is-member path and
/// is used there; it is simply the wrong shape for a 10.000-candidate resolve, which is what this exists for.</para>
/// <para>The effective window is applied by the engine in memory: EffectiveFrom / EffectiveTo are DateTimeOffset (BSON
/// arrays) and never enter a Mongo range filter.</para>
/// </summary>
public sealed class SegmentConsentBulkReader : ISegmentConsentBulkReader
{
    private readonly IMongoCollection<ConsentRecord> _consents;
    private readonly IMongoCollection<PreferenceRecord> _preferences;

    public SegmentConsentBulkReader(IMongoDatabase database)
    {
        _consents = database.GetCollection<ConsentRecord>(ConsentRecordRepository.CollectionName);
        _preferences = database.GetCollection<PreferenceRecord>(PreferenceRecordRepository.CollectionName);
    }

    public async Task<SegmentConsentSnapshot> LoadAsync(
        Guid tenantId,
        string subjectType,
        IReadOnlyCollection<Guid> subjectIds,
        CancellationToken cancellationToken)
    {
        if (subjectIds.Count == 0)
        {
            return new SegmentConsentSnapshot(
                Array.Empty<ConsentRecord>(), Array.Empty<PreferenceRecord>());
        }

        var ids = subjectIds.Distinct().ToList();
        var normalizedSubjectType = SegmentSubjectTypes.Normalize(subjectType);

        var consents = await _consents
            .Find(Builders<ConsentRecord>.Filter.Where(x =>
                x.TenantId == tenantId
                && !x.IsDeleted
                && x.SubjectType == normalizedSubjectType
                && ids.Contains(x.SubjectId)))
            .ToListAsync(cancellationToken);

        var preferences = await _preferences
            .Find(Builders<PreferenceRecord>.Filter.Where(x =>
                x.TenantId == tenantId
                && !x.IsDeleted
                && x.SubjectType == normalizedSubjectType
                && ids.Contains(x.SubjectId)))
            .ToListAsync(cancellationToken);

        return new SegmentConsentSnapshot(consents, preferences);
    }
}
