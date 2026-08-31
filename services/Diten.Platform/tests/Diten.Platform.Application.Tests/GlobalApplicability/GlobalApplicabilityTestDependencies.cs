using Diten.Platform.Application.Features.GlobalApplicability;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Tests.GlobalApplicability;

internal static class GlobalApplicabilityTestDependencies
{
    public static readonly IGlobalApplicabilityTransactionCoordinator Coordinator = new InlineCoordinator();
    public static readonly IGlobalApplicabilityStateRepository State = new NoOpState();
    public static ITransactionalModuleCatalogRepository Module(IModuleCatalogRepository inner) => new ModuleAdapter(inner);

    private sealed class Session : IPlatformTransactionSession { public Guid TransactionId { get; } = Guid.NewGuid(); }
    private sealed class InlineCoordinator : IGlobalApplicabilityTransactionCoordinator
    {
        public async Task<T> ExecuteAsync<T>(GlobalApplicabilityMutationDescriptor descriptor,
            Func<IPlatformTransactionSession, CancellationToken, Task<GlobalApplicabilityMutation<T>>> body,
            CancellationToken cancellationToken = default)
        {
            var session = new Session();
            var mutation = await body(session, cancellationToken);
            if (mutation.EffectiveStateChanged && mutation.WriteProjectionAsync is not null)
                await mutation.WriteProjectionAsync(session, 1, cancellationToken);
            return mutation.Result;
        }

        public async Task<T> ExecuteBatchAsync<T>(
            Func<IPlatformTransactionSession, CancellationToken, Task<GlobalApplicabilityBatchMutation<T>>> body,
            CancellationToken cancellationToken = default)
        {
            var session = new Session();
            var mutation = await body(session, cancellationToken);
            ulong version = 0;
            foreach (var change in mutation.EffectiveChanges)
                await change.WriteProjectionAsync(session, ++version, cancellationToken);
            return mutation.Result;
        }
    }
    private sealed class NoOpState : IGlobalApplicabilityStateRepository
    {
        public Task UpsertSubscriptionPlanAsync(IPlatformTransactionSession session, SubscriptionPlan plan, ulong globalVersion, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertModuleCatalogAsync(IPlatformTransactionSession session, ModuleCatalogItem module, ulong globalVersion, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class ModuleAdapter(IModuleCatalogRepository inner) : ITransactionalModuleCatalogRepository
    {
        public Task<ModuleCatalogItem> CreateAsync(IPlatformTransactionSession session, ModuleCatalogItem item, CancellationToken ct = default) => inner.CreateAsync(item, ct);
        public Task<ModuleCatalogItem?> GetByIdAsync(IPlatformTransactionSession session, Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<ModuleCatalogItem?> GetByCodeAsync(IPlatformTransactionSession session, string code, CancellationToken ct = default) => inner.GetByCodeAsync(code, ct);
        public Task<ModuleCatalogItem?> GetByCodeIncludingDeletedAsync(string code, CancellationToken ct = default) => inner.GetByCodeIncludingDeletedAsync(code, ct);
        public Task<bool> ExistsByCodeAsync(IPlatformTransactionSession session, string code, Guid? excludeId = null, CancellationToken ct = default) => inner.ExistsByCodeAsync(code, excludeId, ct);
        public Task UpdateAsync(IPlatformTransactionSession session, ModuleCatalogItem item, CancellationToken ct = default) => inner.UpdateAsync(item, ct);
        public Task DeleteAsync(IPlatformTransactionSession session, Guid id, CancellationToken ct = default) => inner.DeleteAsync(id, ct);
        public Task<ModuleCatalogItem> CreateAsync(ModuleCatalogItem item, CancellationToken ct = default) => inner.CreateAsync(item, ct);
        public Task<ModuleCatalogItem?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<ModuleCatalogItem?> GetByCodeAsync(string code, CancellationToken ct = default) => inner.GetByCodeAsync(code, ct);
        public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) => inner.ExistsByCodeAsync(code, excludeId, ct);
        public Task UpdateAsync(ModuleCatalogItem item, CancellationToken ct = default) => inner.UpdateAsync(item, ct);
        public Task RestoreAsync(ModuleCatalogItem item, CancellationToken ct = default) => inner.RestoreAsync(item, ct);
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => inner.DeleteAsync(id, ct);
        public Task<(IReadOnlyList<ModuleCatalogItem> Items, long TotalCount)> QueryAsync(ModuleCatalogQuery query, CancellationToken ct = default) => inner.QueryAsync(query, ct);
        public Task<IReadOnlyList<ModuleCatalogItem>> GetAssignableAsync(CancellationToken ct = default) => inner.GetAssignableAsync(ct);
        public Task<IReadOnlyDictionary<Domain.Enums.ModuleCatalogStatus, long>> GetStatsAsync(CancellationToken ct = default) => inner.GetStatsAsync(ct);
    }
}
