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
}
