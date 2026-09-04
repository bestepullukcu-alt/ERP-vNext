using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;
using Diten.Platform.Application.Tests.Authorization;

namespace Diten.Platform.Application.Tests.Tenants.Commercial.Entitlements;

// FIX-QUOTA-DRIFT — modules.max CurrentValue could drift ABOVE the real enabled-entitlement count, so a legitimate
// add/enable was wrongly rejected with 409 QUOTA_LIMIT_EXCEEDED. The Add + Enable handlers now reconcile the counter
// to the real count (RecalculateAsync → ComputeCurrentUsageAsync) BEFORE the enforcing consume, so drift self-heals
// while a GENUINE over-limit still returns 409.
public sealed class TenantModuleEntitlementQuotaDriftTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid EntitlementId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task Add_reconciles_drifted_counter_then_allows_the_add()
    {
        // Free plan: limit 3, REAL enabled = 2, but the counter drifted to 3. The 3rd module must still be addable.
        var quota = new DriftQuotaFake { Current = 3, Limit = 3, TrueEnabledCount = 2 };
        var handler = BuildAddHandler(quota);

        var result = await handler.Handle(AddCommand(), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(3m, quota.Current); // healed 3→2, then consumed → 3 (== limit, allowed)
        Assert.Equal(new[] { "recalc", "consume" }, quota.Calls); // reconcile happens BEFORE the enforcing consume
    }

    [Fact]
    public async Task Add_at_true_limit_still_returns_409()
    {
        // No drift: REAL enabled == limit (3/3). The 4th module is a GENUINE over-limit → must still 409.
        var quota = new DriftQuotaFake { Current = 3, Limit = 3, TrueEnabledCount = 3 };
        var handler = BuildAddHandler(quota);

        var result = await handler.Handle(AddCommand(), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains(QuotaErrorCodes.LimitExceeded, result.Errors);
    }

    [Fact]
    public async Task Enable_reconciles_drifted_counter_then_allows_the_enable()
    {
        // Re-enabling a disabled module with a drifted counter (3 while real enabled = 2) must succeed.
        var quota = new DriftQuotaFake { Current = 3, Limit = 3, TrueEnabledCount = 2 };
        var entity = new TenantModuleEntitlement
        {
            Id = EntitlementId,
            TenantId = TenantId,
            ModuleCode = "CRM",
            Source = EntitlementSource.ManualOverride,
            IsEnabled = false,
            RowVersion = Guid.Parse("11111111-1111-1111-1111-111111111111").ToByteArray()
        };

        var repo = new Mock<ITenantModuleEntitlementRepository>();
        repo.Setup(x => x.GetByIdAsync(TenantId, EntitlementId, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        repo.Setup(x => x.UpdateAsync(It.IsAny<TenantModuleEntitlement>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var eventBus = new Mock<IEventBus>();
        eventBus.Setup(x => x.PublishAsync(It.IsAny<TenantEntitlementEnabledV1>(), It.IsAny<EventPublishOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<EventEnvelope<TenantEntitlementEnabledV1>>(null!));
        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var dependencies = PhysicalHandlerTestDependencies.Create(repo, quota, eventBus.Object);
        var handler = new EnableTenantModuleEntitlementCommandHandler(repo.Object, quota, dependencies.Executor,
            dependencies.Versions, dependencies.Events, dependencies.Audit, currentUser.Object);

        var result = await handler.Handle(new EnableTenantModuleEntitlementCommand(TenantId, EntitlementId, entity.RowVersion), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(new[] { "recalc", "consume" }, quota.Calls);
        Assert.True(entity.IsEnabled);
    }

    private static AddTenantModuleEntitlementCommand AddCommand() =>
        new(TenantId, new TenantModuleEntitlementRequest("CRM", EntitlementSource.ManualOverride, true, null, "add", null));

    private static AddTenantModuleEntitlementCommandHandler BuildAddHandler(IQuotaService quota)
    {
        var repo = new Mock<ITenantModuleEntitlementRepository>();
        repo.Setup(x => x.GetByTenantAndModuleAsync(TenantId, "CRM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantModuleEntitlement>()); // no active conflict
        repo.Setup(x => x.CreateAsync(It.IsAny<TenantModuleEntitlement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantModuleEntitlement e, CancellationToken _) => e);

        var moduleRepo = new Mock<IModuleCatalogRepository>();
        moduleRepo.Setup(x => x.GetByCodeAsync("CRM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleCatalogItem { ModuleCode = "CRM", ModuleName = "CRM", DisplayName = "CRM", Status = ModuleCatalogStatus.Active });

        var eventBus = new Mock<IEventBus>();
        eventBus.Setup(x => x.PublishAsync(It.IsAny<TenantEntitlementAddedV1>(), It.IsAny<EventPublishOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<EventEnvelope<TenantEntitlementAddedV1>>(null!));

        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var dependencies = PhysicalHandlerTestDependencies.Create(repo, quota, eventBus.Object);
        return new AddTenantModuleEntitlementCommandHandler(repo.Object, moduleRepo.Object, quota, dependencies.Executor,
            dependencies.Versions, dependencies.Events, dependencies.Audit, currentUser.Object);
    }

    // Models the two primitives the fix relies on: RecalculateAsync HEALS CurrentValue to the real enabled count;
    // TryConsumeAsync enforces the limit against the (healed) value. Records call order to prove reconcile-before-consume.
    private sealed class DriftQuotaFake : IQuotaService
    {
        public decimal Current { get; set; }
        public decimal Limit { get; set; }
        public decimal TrueEnabledCount { get; set; }
        public List<string> Calls { get; } = new();

        public Task<Response<QuotaStatusDto>> RecalculateAsync(RecalculateQuotaUsageRequest request, CancellationToken ct)
        {
            Calls.Add("recalc");
            Current = TrueEnabledCount; // heal drift to the real enabled-entitlement count
            return Task.FromResult(Response<QuotaStatusDto>.Success(Status()));
        }

        public Task<Response<QuotaMutationDto>> TryConsumeAsync(TryConsumeQuotaRequest request, CancellationToken ct)
        {
            Calls.Add("consume");
            if (Current + request.Amount > Limit)
            {
                return Task.FromResult(Response<QuotaMutationDto>.Fail(QuotaErrorCodes.LimitExceeded, 409));
            }
            Current += request.Amount;
            return Task.FromResult(Response<QuotaMutationDto>.Success(new QuotaMutationDto(request.TenantId, "modules.max", Current, Limit, request.Amount, true, null)));
        }

        private QuotaStatusDto Status() => new(
            TenantId, "modules.max", Current, Limit, 0, false, false,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "Test", null, null, null, false, false, null, null);

        // Unused by the Add/Enable handlers.
        public Task<Response<QuotaMutationDto>> ReleaseAsync(ReleaseQuotaRequest request, CancellationToken ct) => throw new NotImplementedException();
        public Task<Response<QuotaMutationDto>> TryConsumeEntitlementAsync(IPlatformTransactionSession session, TryConsumeQuotaRequest request, CancellationToken ct) => TryConsumeAsync(request, ct);
        public Task<Response<QuotaMutationDto>> ReleaseEntitlementAsync(IPlatformTransactionSession session, ReleaseQuotaRequest request, CancellationToken ct) => ReleaseAsync(request, ct);
        public Task<Response<QuotaStatusDto>> RecalculateEntitlementAsync(IPlatformTransactionSession session, RecalculateQuotaUsageRequest request, CancellationToken ct) => RecalculateAsync(request, ct);
        public Task<bool> TryConsumeAsync(Guid tenantId, string quotaKey, decimal amount, CancellationToken ct) => throw new NotImplementedException();
        public Task<QuotaStatusDto> GetStatusAsync(Guid tenantId, string quotaKey) => throw new NotImplementedException();
        public Task ReleaseAsync(Guid tenantId, string quotaKey, decimal amount) => throw new NotImplementedException();
        public Task<Response<IReadOnlyList<QuotaStatusDto>>> GetStatusesAsync(Guid tenantId, CancellationToken ct) => throw new NotImplementedException();
        public Task<Response<QuotaStatusDto>> GetStatusResponseAsync(Guid tenantId, string quotaKey, CancellationToken ct) => throw new NotImplementedException();
        public Task<Response<IReadOnlyList<QuotaStatusDto>>> InitializeTenantQuotasAsync(Guid tenantId, string source, string reason, string actorId, string correlationId, CancellationToken ct) => throw new NotImplementedException();
        public Task<Response<IReadOnlyList<QuotaStatusDto>>> SyncTenantQuotaLimitsAsync(Guid tenantId, string source, string reason, string actorId, string correlationId, CancellationToken ct) => throw new NotImplementedException();
        public Task<Response<QuotaStatusDto>> ResetPeriodAsync(ResetQuotaPeriodRequest request, CancellationToken ct) => throw new NotImplementedException();
    }
}
