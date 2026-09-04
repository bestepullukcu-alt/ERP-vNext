using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class GlobalApplicabilityStateRepository : IGlobalApplicabilityStateRepository
{
    public const string CollectionName = "global_applicability_state_v1";
    private readonly IPlatformDbContext _dbContext;
    private readonly IMongoCollection<GlobalApplicabilityState> _collection;

    public GlobalApplicabilityStateRepository(IPlatformDbContext dbContext)
    {
        _dbContext = dbContext;
        _collection = dbContext.GetCollection<GlobalApplicabilityState>(CollectionName);
    }

    public Task UpsertSubscriptionPlanAsync(
        IPlatformTransactionSession session,
        SubscriptionPlan plan,
        ulong globalVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        EnsureVersion(globalVersion);
        var state = new GlobalApplicabilityState
        {
            Id = $"plan:{plan.Id:D}",
            Kind = "subscription-plan",
            SourceId = plan.Id,
            Code = NormalizeCode(plan.Code),
            IsDeleted = plan.IsDeleted,
            IsActive = plan.IsActive && !plan.IsDeleted,
            IncludedModuleKeys = plan.IncludedModuleKeys
                .Select(NormalizeCode)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray(),
            GlobalVersion = globalVersion,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        return UpsertAsync(session, state, cancellationToken);
    }

    public Task UpsertModuleCatalogAsync(
        IPlatformTransactionSession session,
        ModuleCatalogItem module,
        ulong globalVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(module);
        EnsureVersion(globalVersion);
        var state = new GlobalApplicabilityState
        {
            Id = $"module:{module.Id:D}",
            Kind = "module-catalog",
            SourceId = module.Id,
            Code = NormalizeCode(module.ModuleCode),
            IsDeleted = module.IsDeleted,
            IsActive = module.Status == ModuleCatalogStatus.Active && !module.IsDeleted,
            IsBaseline = module.IsBaseline,
            IsCoreModule = module.IsCoreModule,
            IsTenantAssignable = module.IsTenantAssignable,
            GlobalVersion = globalVersion,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        return UpsertAsync(session, state, cancellationToken);
    }

    private async Task UpsertAsync(
        IPlatformTransactionSession session,
        GlobalApplicabilityState state,
        CancellationToken cancellationToken)
    {
        var handle = PlatformMongoTransactionSession.Require(session, _dbContext);
        await _collection.ReplaceOneAsync(
            handle,
            Builders<GlobalApplicabilityState>.Filter.Eq(x => x.Id, state.Id),
            state,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    private static void EnsureVersion(ulong globalVersion)
    {
        if (globalVersion == 0 || globalVersion > long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(globalVersion), "A positive Mongo Int64 applicability version is required.");
        }
    }

    private static string NormalizeCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return code.Trim().Normalize().ToUpperInvariant();
    }
}
