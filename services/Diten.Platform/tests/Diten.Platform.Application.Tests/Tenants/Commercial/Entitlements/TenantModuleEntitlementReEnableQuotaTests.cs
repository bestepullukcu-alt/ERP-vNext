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

namespace Diten.Platform.Application.Tests.Tenants.Commercial.Entitlements;

// FIX-ENTITLEMENT-REENABLE — a module could only ever be enabled ONCE: the quota consume used a dedup key that
// was deterministic for the entitlement's whole lifetime, so re-enabling after a disable was rejected as a
// duplicate operation (409). The fix scopes the key to each enable/disable EVENT (the row's RowVersion). These
// tests exercise the real handlers against a quota fake that dedups on OperationId exactly like the live service.
public sealed class TenantModuleEntitlementReEnableQuotaTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid EntitlementId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task Enable_Disable_ReEnable_Succeeds_AndConsumesModulesMaxExactlyOnce()
    {
        var entity = new TenantModuleEntitlement
        {
            Id = EntitlementId,
            TenantId = TenantId,
            ModuleCode = "HR",
            Source = EntitlementSource.ManualOverride,
            IsEnabled = false,
            RowVersion = Guid.Parse("11111111-1111-1111-1111-111111111111").ToByteArray()
        };

        var repo = BuildStatefulRepo(entity);
        var quota = new DedupingQuotaFake();
        var moduleRepo = BuildModuleRepo();
        var eventBus = BuildEventBus();
        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var enable = new EnableTenantModuleEntitlementCommandHandler(repo, quota, eventBus, currentUser.Object);
        var disable = new DisableTenantModuleEntitlementCommandHandler(repo, moduleRepo, quota, eventBus, currentUser.Object);

        // 1) Enable (from disabled) — consumes one ModulesMax slot.
        var r1 = await enable.Handle(new EnableTenantModuleEntitlementCommand(TenantId, EntitlementId, entity.RowVersion), CancellationToken.None);
        Assert.True(r1.IsSuccessful);
        Assert.Equal(1, quota.Consumed);

        // 2) Disable — releases the slot back.
        var r2 = await disable.Handle(
            new DisableTenantModuleEntitlementCommand(TenantId, new DisableTenantModuleEntitlementRequest("HR", EntitlementId, "disable", entity.RowVersion)),
            CancellationToken.None);
        Assert.True(r2.IsSuccessful);
        Assert.Equal(0, quota.Consumed);

        // 3) RE-ENABLE — previously 409 QUOTA_DUPLICATE_OPERATION (permanent dead button). Must now succeed.
        var r3 = await enable.Handle(new EnableTenantModuleEntitlementCommand(TenantId, EntitlementId, entity.RowVersion), CancellationToken.None);
        Assert.True(r3.IsSuccessful);
        Assert.Equal(204, r3.StatusCode);

        // Quota count is correct across the whole cycle: consumed exactly once, never double-counted or drifted.
        Assert.Equal(1, quota.Consumed);
        Assert.True(entity.IsEnabled);
    }

    [Fact]
    public async Task ReEnable_Blocked_ByRealQuotaLimit_ReturnsFailureThatSurfacesToTheUser()
    {
        // When the block is a GENUINE quota exhaustion (not the lifetime-dedup bug), the handler must return the
        // failure (which the frontend now turns into an error toast) rather than silently succeeding.
        var entity = new TenantModuleEntitlement
        {
            Id = EntitlementId,
            TenantId = TenantId,
            ModuleCode = "HR",
            Source = EntitlementSource.ManualOverride,
            IsEnabled = false,
            RowVersion = Guid.Parse("22222222-2222-2222-2222-222222222222").ToByteArray()
        };

        var repo = BuildStatefulRepo(entity);
        var quota = new DedupingQuotaFake { FailWith = QuotaErrorCodes.LimitExceeded };
        var eventBus = BuildEventBus();
        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var enable = new EnableTenantModuleEntitlementCommandHandler(repo, quota, eventBus, currentUser.Object);

        var result = await enable.Handle(new EnableTenantModuleEntitlementCommand(TenantId, EntitlementId, entity.RowVersion), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains(QuotaErrorCodes.LimitExceeded, result.Errors);
        Assert.False(entity.IsEnabled); // not flipped when the consume fails
    }

    // Moq repo whose UpdateAsync bumps RowVersion (mirroring the real repo) and whose GetByIdAsync returns a clone
    // of the CURRENT persisted state, so each enable/disable cycle observes a fresh RowVersion.
    private static ITenantModuleEntitlementRepository BuildStatefulRepo(TenantModuleEntitlement current)
    {
        var repo = new Mock<ITenantModuleEntitlementRepository>();
        repo.Setup(x => x.GetByIdAsync(TenantId, EntitlementId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Clone(current));
        repo.Setup(x => x.UpdateAsync(It.IsAny<TenantModuleEntitlement>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()))
            .Returns((TenantModuleEntitlement e, byte[]? _, CancellationToken __) =>
            {
                var bumped = Guid.NewGuid().ToByteArray();
                e.RowVersion = bumped;          // real repo mutates the passed entity's RowVersion
                current.IsEnabled = e.IsEnabled;
                current.Reason = e.Reason;
                current.RowVersion = bumped;    // persist
                return Task.CompletedTask;
            });
        return repo.Object;
    }

    private static TenantModuleEntitlement Clone(TenantModuleEntitlement e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        ModuleCode = e.ModuleCode,
        Source = e.Source,
        IsEnabled = e.IsEnabled,
        ExpiryDateUtc = e.ExpiryDateUtc,
        Reason = e.Reason,
        RowVersion = (byte[])e.RowVersion.Clone()
    };

    private static IModuleCatalogRepository BuildModuleRepo()
    {
        var moduleRepo = new Mock<IModuleCatalogRepository>();
        moduleRepo.Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken _) => new ModuleCatalogItem
            {
                ModuleCode = code,
                ModuleName = code,
                DisplayName = code,
                Status = ModuleCatalogStatus.Active
            });
        return moduleRepo.Object;
    }

    private static IEventBus BuildEventBus()
    {
        var bus = new Mock<IEventBus>();
        bus.Setup(x => x.PublishAsync(It.IsAny<TenantEntitlementEnabledV1>(), It.IsAny<EventPublishOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<EventEnvelope<TenantEntitlementEnabledV1>>(null!));
        bus.Setup(x => x.PublishAsync(It.IsAny<TenantEntitlementDisabledV1>(), It.IsAny<EventPublishOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<EventEnvelope<TenantEntitlementDisabledV1>>(null!));
        return bus.Object;
    }

    // Fake quota service that dedups on OperationId exactly like the live QuotaService (a non-rejected event with
    // the same OperationId → QUOTA_DUPLICATE_OPERATION), and tracks the running ModulesMax consumed count.
    private sealed class DedupingQuotaFake : IQuotaService
    {
        private readonly HashSet<string> _seenOperationIds = new(StringComparer.Ordinal);
        public int Consumed { get; private set; }
        public string? FailWith { get; init; }

        public Task<Response<QuotaMutationDto>> TryConsumeAsync(TryConsumeQuotaRequest request, CancellationToken ct)
        {
            if (FailWith is not null)
            {
                return Task.FromResult(Response<QuotaMutationDto>.Fail(FailWith, 409));
            }
            if (request.OperationId is not null && !_seenOperationIds.Add(request.OperationId))
            {
                return Task.FromResult(Response<QuotaMutationDto>.Fail(QuotaErrorCodes.DuplicateOperation, 409));
            }
            Consumed++;
            return Task.FromResult(Response<QuotaMutationDto>.Success(new QuotaMutationDto(request.TenantId, "modules.max", Consumed, 10, 1, true, null)));
        }

        public Task<Response<QuotaMutationDto>> ReleaseAsync(ReleaseQuotaRequest request, CancellationToken ct)
        {
            if (request.OperationId is not null && !_seenOperationIds.Add(request.OperationId))
            {
                return Task.FromResult(Response<QuotaMutationDto>.Fail(QuotaErrorCodes.DuplicateOperation, 409));
            }
            Consumed--;
            return Task.FromResult(Response<QuotaMutationDto>.Success(new QuotaMutationDto(request.TenantId, "modules.max", Consumed, 10, -1, true, null)));
        }

        // FIX-QUOTA-DRIFT — the enable handler now reconciles before consuming; this fake has no drift, so recalc is
        // a benign no-op that leaves the consumed count unchanged (returns the current value as CurrentValue).
        public Task<Response<QuotaStatusDto>> RecalculateAsync(RecalculateQuotaUsageRequest request, CancellationToken ct)
            => Task.FromResult(Response<QuotaStatusDto>.Success(new QuotaStatusDto(
                request.TenantId, "modules.max", Consumed, 10, 0, false, false,
                DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "Test", null, null, null, false, false, null, null)));

        // Unused by the enable/disable handlers.
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
