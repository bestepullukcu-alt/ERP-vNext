using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Domain.Recall.Entities;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Recall;

/// <summary>
/// WP-0177-R4 — <see cref="IRecallSubjectDirectory"/> (the MOD-0176 <c>ExpirySubjectDirectory</c> pattern): TENANT-filtered, company-free reads of a
/// case / a prepared settings change (company + owner only), so the provider can set the company scope from the record. Another tenant's id
/// finds nothing (K3 sabotage (g): drop the tenant filter → the provider's cross-tenant test is red). R5: <c>recall.drill-run</c> (company only).
/// </summary>
public sealed class RecallSubjectDirectory : IRecallSubjectDirectory
{
    private readonly IMongoCollection<RecallCase> _cases;
    private readonly IMongoCollection<RecallSettingsChange> _changes;
    private readonly IMongoCollection<RecallDrillRun> _runs;

    public RecallSubjectDirectory(IMongoDatabase database)
    {
        _cases = database.GetCollection<RecallCase>(RecallCollections.Cases);
        _changes = database.GetCollection<RecallSettingsChange>(RecallCollections.SettingsChanges);
        _runs = database.GetCollection<RecallDrillRun>(RecallCollections.DrillRuns);
    }

    public async Task<(Guid LegalEntityId, Guid? Owner)?> SubjectAsync(Guid tenantId, string subjectType, Guid subjectId, CancellationToken cancellationToken = default)
    {
        if (subjectType == RecallSignableActions.CaseSubjectType)
        {
            var c = await _cases.Find(Builders<RecallCase>.Filter.Eq(x => x.TenantId, tenantId) & Builders<RecallCase>.Filter.Eq(x => x.IsDeleted, false)
                                      & Builders<RecallCase>.Filter.Eq(x => x.Id, subjectId)).FirstOrDefaultAsync(cancellationToken);
            return c?.LegalEntityId is { } company ? (company, null) : null;
        }

        if (subjectType == RecallSignableActions.SettingsChangeSubjectType)
        {
            var change = await _changes.Find(Builders<RecallSettingsChange>.Filter.Eq(x => x.TenantId, tenantId) & Builders<RecallSettingsChange>.Filter.Eq(x => x.IsDeleted, false)
                                              & Builders<RecallSettingsChange>.Filter.Eq(x => x.Id, subjectId)).FirstOrDefaultAsync(cancellationToken);
            return change?.LegalEntityId is { } company ? (company, change.SetBy) : null;
        }

        if (subjectType == RecallSignableActions.DrillRunSubjectType)
        {
            var run = await _runs.Find(Builders<RecallDrillRun>.Filter.Eq(x => x.TenantId, tenantId) & Builders<RecallDrillRun>.Filter.Eq(x => x.IsDeleted, false)
                                       & Builders<RecallDrillRun>.Filter.Eq(x => x.Id, subjectId)).FirstOrDefaultAsync(cancellationToken);
            return run?.LegalEntityId is { } company ? (company, null) : null; // R5: a run of the tenant, its own company
        }

        return null;
    }
}
