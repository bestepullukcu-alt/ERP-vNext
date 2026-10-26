using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Repositories;
using MongoDB.Driver;
using System.Linq.Expressions;

namespace Diten.InventoryService.Persistence.Recall;

/// <summary>
/// WP-MVP1-NAMES1 — <see cref="IRecallActorRecords"/> over the stores the recall writes already fill (case events, drill events, settings
/// history, <c>inventory_audit</c>): ONE query per store for all the asked pairs (an <c>$or</c> of one-second windows per user), tenant +
/// company + not deleted, read only.
/// </summary>
public sealed class RecallActorRecords : IRecallActorRecords
{
    private readonly IMongoCollection<RecallCaseEvent> _events;
    private readonly IMongoCollection<RecallDrillEvent> _drillEvents;
    private readonly IMongoCollection<RecallAuditRecord> _audit;
    private readonly IMongoCollection<RecallSettingsHistoryEntry> _history;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public RecallActorRecords(IMongoDatabase database, ITenantContext tenant)
    {
        _events = database.GetCollection<RecallCaseEvent>(RecallCollections.CaseEvents);
        _drillEvents = database.GetCollection<RecallDrillEvent>(RecallCollections.DrillEvents);
        _audit = database.GetCollection<RecallAuditRecord>(InventoryCollections.Audit);
        _history = database.GetCollection<RecallSettingsHistoryEntry>(RecallCollections.SettingsHistory);
        _tenantId = tenant.TenantId;
        _legalEntityId = tenant.LegalEntityId;
    }

    private FilterDefinition<T> Scope<T>() where T : EntityBase
        => Builders<T>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<T>.Filter.Eq(x => x.LegalEntityId, _legalEntityId)
           & Builders<T>.Filter.Eq(x => x.IsDeleted, false);

    public async Task<RecallActorRecordSet> AtAsync(IReadOnlyCollection<(Guid UserId, DateTimeOffset Second)> pairs, CancellationToken cancellationToken = default)
    {
        if (pairs.Count == 0) return new RecallActorRecordSet([], [], [], []);
        var distinct = pairs.Distinct().ToList();
        return new RecallActorRecordSet(
            await ReadAsync(_events, distinct, e => e.ActorUserId, e => e.AtUtc, cancellationToken),
            await ReadAsync(_drillEvents, distinct, e => e.ActorUserId, e => e.AtUtc, cancellationToken),
            await ReadAsync(_audit, distinct, a => a.UserId, a => a.OccurredAt, cancellationToken),
            await ReadAsync(_history, distinct, h => h.ChangedBy, h => h.ChangedAtUtc, cancellationToken));
    }

    public async Task<IReadOnlyList<RecallAuditRecord>> AuditRecordsAsync(IReadOnlyCollection<Guid> subjectIds, CancellationToken cancellationToken = default)
        => subjectIds.Count == 0 ? [] : await _audit.Find(Scope<RecallAuditRecord>() & Builders<RecallAuditRecord>.Filter.In(x => x.SubjectId, subjectIds)).ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<T>> ReadAsync<T, TUser>(IMongoCollection<T> collection, IReadOnlyList<(Guid UserId, DateTimeOffset Second)> pairs,
        Expression<Func<T, TUser>> user, Expression<Func<T, DateTimeOffset>> at, CancellationToken cancellationToken) where T : EntityBase
    {
        var f = Builders<T>.Filter;
        var windows = pairs.Select(p => f.Eq(user, (TUser)(object)p.UserId) & f.Gte(at, p.Second) & f.Lt(at, p.Second.AddSeconds(1)));
        return await collection.Find(Scope<T>() & f.Or(windows)).ToListAsync(cancellationToken);
    }
}
