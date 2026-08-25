using Diten.Platform.Application.Features.EntitlementAttestations;
using Diten.Platform.Application.Services;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Authorization;

public sealed class MongoAuthoritativeEntitlementDecisionSource : IAuthoritativeEntitlementDecisionSource
{
    private const string GlobalKey = "global:catalog-applicability";
    private readonly IPlatformDbContext _context;
    private readonly ITenantModuleAccessService _access;
    private readonly IModuleCatalogRepository _modules;

    public MongoAuthoritativeEntitlementDecisionSource(IPlatformDbContext context, ITenantModuleAccessService access, IModuleCatalogRepository modules)
    { _context = context; _access = access; _modules = modules; }

    public async Task<EntitlementStateVersionV1> ReadCurrentVersionAsync(Guid tenantId, string normalizedModuleCode, CancellationToken cancellationToken)
    {
        var collection = _context.GetCollection<BsonDocument>(EntitlementStateVersionRepository.CollectionName);
        var keys = new[] { $"physical:{tenantId:D}:{normalizedModuleCode}", $"subscription:{tenantId:D}", GlobalKey };
        var rows = await collection.Find(Builders<BsonDocument>.Filter.In("_id", keys)).ToListAsync(cancellationToken);
        var values = rows.ToDictionary(x => x["_id"].AsString, x => ReadPositiveVersion(x), StringComparer.Ordinal);
        return new(
            values.GetValueOrDefault(keys[0]),
            values.GetValueOrDefault(keys[1]),
            values.GetValueOrDefault(keys[2]));
    }

    public async Task<EntitlementDecisionSnapshotV1> ReadAsync(Guid tenantId, string normalizedModuleCode, string requestHash, CancellationToken cancellationToken)
    {
        var version = await ReadCurrentVersionAsync(tenantId, normalizedModuleCode, cancellationToken);
        if (!version.IsComplete) throw new FormatException("Entitlement version authority is incomplete.");
        var module = await _modules.GetByCodeAsync(normalizedModuleCode, cancellationToken);
        EntitlementDecisionV1 decision;
        if (module is null || module.IsDeleted || module.Status != ModuleCatalogStatus.Active || (!module.IsTenantAssignable && !module.IsBaseline))
            decision = EntitlementDecisionV1.NotApplicable;
        else
        {
            var detail = await _access.GetEffectiveAccessDetailAsync(tenantId, normalizedModuleCode, cancellationToken);
            decision = MapEffectiveAccess(detail.EffectiveAccess);
        }
        return new(tenantId, normalizedModuleCode, requestHash, decision, version, DateTimeOffset.UtcNow);
    }

    public static EntitlementDecisionV1 MapEffectiveAccess(TenantModuleEffectiveAccess value) => value switch
    {
        TenantModuleEffectiveAccess.Active or TenantModuleEffectiveAccess.EnabledByOverride or TenantModuleEffectiveAccess.SystemLocked => EntitlementDecisionV1.Active,
        TenantModuleEffectiveAccess.BlockedByOverride => EntitlementDecisionV1.Disabled,
        TenantModuleEffectiveAccess.Expired => EntitlementDecisionV1.Expired,
        _ => EntitlementDecisionV1.Missing
    };

    private static ulong ReadPositiveVersion(BsonDocument value)
    {
        if (!value.TryGetValue("Value", out var raw) && !value.TryGetValue("value", out raw)) throw new FormatException("Version component is missing.");
        if (!raw.IsInt64 || raw.AsInt64 <= 0) throw new FormatException("Version component is invalid.");
        return checked((ulong)raw.AsInt64);
    }
}
