using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Moq;

namespace Diten.Platform.Application.Tests.Authorization;

internal sealed record PhysicalHandlerTestDependencies(
    IPlatformTransactionExecutor Executor,
    IEntitlementStateVersionRepository Versions,
    ITransactionalIntegrationEventWriter Events,
    ITransactionalAuditOutboxWriter Audit)
{
    public static PhysicalHandlerTestDependencies Create(
        Mock<ITenantModuleEntitlementRepository> repository,
        Mock<IQuotaService> quota,
        IEventBus eventBus)
    {
        repository.Setup(x => x.CreateAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<TenantModuleEntitlement>(), It.IsAny<CancellationToken>()))
            .Returns((IPlatformTransactionSession _, TenantModuleEntitlement entity, CancellationToken ct) => repository.Object.CreateAsync(entity, ct));
        repository.Setup(x => x.UpdateAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<TenantModuleEntitlement>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()))
            .Returns((IPlatformTransactionSession _, TenantModuleEntitlement entity, byte[]? version, CancellationToken ct) => repository.Object.UpdateAsync(entity, version, ct));
        repository.Setup(x => x.SoftDeleteAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()))
            .Returns((IPlatformTransactionSession _, Guid tenantId, Guid id, byte[]? version, CancellationToken ct) => repository.Object.SoftDeleteAsync(tenantId, id, version, ct));

        quota.Setup(x => x.TryConsumeEntitlementAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<TryConsumeQuotaRequest>(), It.IsAny<CancellationToken>()))
            .Returns((IPlatformTransactionSession _, TryConsumeQuotaRequest request, CancellationToken ct) => quota.Object.TryConsumeAsync(request, ct));
        quota.Setup(x => x.ReleaseEntitlementAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<ReleaseQuotaRequest>(), It.IsAny<CancellationToken>()))
            .Returns((IPlatformTransactionSession _, ReleaseQuotaRequest request, CancellationToken ct) => quota.Object.ReleaseAsync(request, ct));
        quota.Setup(x => x.RecalculateEntitlementAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<RecalculateQuotaUsageRequest>(), It.IsAny<CancellationToken>()))
            .Returns((IPlatformTransactionSession _, RecalculateQuotaUsageRequest request, CancellationToken ct) => quota.Object.RecalculateAsync(request, ct));

        var versions = new Mock<IEntitlementStateVersionRepository>();
        versions.Setup(x => x.IncrementPhysicalEntitlementVersionAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1UL);
        return new(new ImmediateExecutor(), versions.Object, new ForwardingEventWriter(eventBus), new SuccessfulAuditWriter());
    }

    public static PhysicalHandlerTestDependencies Create(
        Mock<ITenantModuleEntitlementRepository> repository,
        IQuotaService quota,
        IEventBus eventBus)
    {
        ForwardRepositoryMutations(repository);
        var versions = VersionRepository();
        return new(new ImmediateExecutor(), versions, new ForwardingEventWriter(eventBus), new SuccessfulAuditWriter());
    }

    private static void ForwardRepositoryMutations(Mock<ITenantModuleEntitlementRepository> repository)
    {
        repository.Setup(x => x.CreateAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<TenantModuleEntitlement>(), It.IsAny<CancellationToken>()))
            .Returns((IPlatformTransactionSession _, TenantModuleEntitlement entity, CancellationToken ct) => repository.Object.CreateAsync(entity, ct));
        repository.Setup(x => x.UpdateAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<TenantModuleEntitlement>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()))
            .Returns((IPlatformTransactionSession _, TenantModuleEntitlement entity, byte[]? version, CancellationToken ct) => repository.Object.UpdateAsync(entity, version, ct));
        repository.Setup(x => x.SoftDeleteAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()))
            .Returns((IPlatformTransactionSession _, Guid tenantId, Guid id, byte[]? version, CancellationToken ct) => repository.Object.SoftDeleteAsync(tenantId, id, version, ct));
    }

    private static IEntitlementStateVersionRepository VersionRepository()
    {
        var versions = new Mock<IEntitlementStateVersionRepository>();
        versions.Setup(x => x.IncrementPhysicalEntitlementVersionAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1UL);
        return versions.Object;
    }

    private sealed class Session : IPlatformTransactionSession { public Guid TransactionId { get; } = Guid.NewGuid(); }
    private sealed class ImmediateExecutor : IPlatformTransactionExecutor
    {
        public Task<T> ExecuteAsync<T>(Func<IPlatformTransactionSession, CancellationToken, Task<T>> body, CancellationToken cancellationToken = default) =>
            body(new Session(), cancellationToken);
    }

    private sealed class ForwardingEventWriter(IEventBus bus) : ITransactionalIntegrationEventWriter
    {
        public Task<EventEnvelope<TEvent>> EnqueueAsync<TEvent>(IPlatformTransactionSession session, TEvent @event, EventPublishOptions options, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent => bus.PublishAsync(@event, options, cancellationToken);
    }

    private sealed class SuccessfulAuditWriter : ITransactionalAuditOutboxWriter
    {
        public Task<bool> TryEnqueueAsync(IPlatformTransactionSession session, AuditOutboxWriteRequest request, CancellationToken ct = default) => Task.FromResult(true);
    }
}
