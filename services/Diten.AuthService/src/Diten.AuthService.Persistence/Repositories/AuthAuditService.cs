using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class AuthAuditService : IAuthAuditService
{
    private readonly IMongoCollection<AuthAuditLog> _collection;

    public AuthAuditService(IMongoDatabase database)
    {
        _collection = database.GetCollection<AuthAuditLog>("authAuditLogs");
    }

    public async Task WriteEmptyRoleLoginAsync(Guid userId, Guid tenantId, string email, CancellationToken ct = default)
    {
        var metadata = $"{{\"email\":\"{email}\",\"reason\":\"empty_role_set\"}}";
        var entry = new AuthAuditLog("auth.login.empty_roles", userId, tenantId, metadata);
        await _collection.InsertOneAsync(entry, cancellationToken: ct);
    }

    public async Task WriteAsync(string eventName, Guid? userId, Guid tenantId, string metadata, CancellationToken ct = default)
    {
        var entry = new AuthAuditLog(eventName, userId ?? Guid.Empty, tenantId, metadata);
        await _collection.InsertOneAsync(entry, cancellationToken: ct);
    }

    public async Task<bool> WriteOnceAsync(
        Guid auditId, string eventName, Guid? userId, Guid tenantId, string metadata, CancellationToken ct = default)
    {
        if (auditId == Guid.Empty) throw new ArgumentException("An audit id is required.", nameof(auditId));

        var entry = new AuthAuditLog(eventName, userId ?? Guid.Empty, tenantId, metadata) { Id = auditId };
        try
        {
            // Insert only: the id is the document's _id, which MongoDB keeps unique on every collection.
            await _collection.InsertOneAsync(entry, cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // "Already recorded" only if the row with THIS id is really there. A duplicate key from anything else
            // (a unique index added to this collection later) must not pass for a written audit row.
            var recorded = await _collection.CountDocumentsAsync(
                Builders<AuthAuditLog>.Filter.Eq(x => x.Id, auditId), new CountOptions { Limit = 1 }, ct);
            if (recorded == 1) return false;
            throw;
        }
    }
}
