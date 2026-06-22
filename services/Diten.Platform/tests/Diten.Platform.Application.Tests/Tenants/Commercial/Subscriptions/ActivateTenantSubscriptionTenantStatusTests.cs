using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Handlers.CommandHandlers;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants.Commercial.Subscriptions;

// C3a: subscription activation is the deliberate go-live action that promotes a still-provisioning tenant
// to Active. The promotion is flag-gated (markTenantActive) so only this path touches tenant lifecycle
// Status; cancel/suspend/renew/expire/reactivate snapshot updates must leave it untouched.
public sealed class ActivateTenantSubscriptionTenantStatusTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SubscriptionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PlanId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly byte[] RowVersion = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee").ToByteArray();

    [Fact]
    public async Task Activate_PromotesProvisioningTenantToActive_AndFinalizesProvisioning()
    {
        var fixture = CreateActivateFixture(TenantSubscriptionStatus.Trialing, TenantStatus.Provisioning);

        var result = await fixture.Handler.Handle(CreateActivateCommand(), CancellationToken.None);

        Assert.True(result.IsSuccessful);

        var savedSubscription = fixture.CapturedSubscription();
        Assert.NotNull(savedSubscription);
        Assert.Equal(TenantSubscriptionStatus.Active, savedSubscription!.Status);

        var savedTenant = fixture.CapturedTenant();
        Assert.NotNull(savedTenant);
        Assert.Equal(TenantStatus.Active, savedTenant!.Status);
        Assert.NotNull(savedTenant.ProvisionedAt);
        Assert.NotNull(savedTenant.ActivatedAt);
        Assert.Equal("Completed", savedTenant.ProvisioningStatus);
        Assert.All(savedTenant.ProvisioningSteps, step => Assert.Equal("Completed", step.Status));
    }

    [Fact]
    public async Task Activate_DoesNotPromoteSuspendedTenant()
    {
        // Security: a Suspended tenant must never be silently resurrected to Active by a subscription activate.
        var fixture = CreateActivateFixture(TenantSubscriptionStatus.Trialing, TenantStatus.Suspended);

        var result = await fixture.Handler.Handle(CreateActivateCommand(), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var savedTenant = fixture.CapturedTenant();
        Assert.NotNull(savedTenant);
        Assert.Equal(TenantStatus.Suspended, savedTenant!.Status);
    }

    [Fact]
    public async Task Suspend_DoesNotChangeTenantLifecycleStatus()
    {
        // The snapshot update for suspend does NOT pass markTenantActive → tenant Status is left as-is.
        var fixture = CreateSuspendFixture(TenantStatus.Provisioning);

        var result = await fixture.Handler.Handle(CreateSuspendCommand(), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var savedTenant = fixture.CapturedTenant();
        Assert.NotNull(savedTenant);
        Assert.Equal(TenantStatus.Provisioning, savedTenant!.Status);
    }

    private static ActivateFixture CreateActivateFixture(TenantSubscriptionStatus subscriptionStatus, TenantStatus tenantStatus)
    {
        Tenant? capturedTenant = null;
        TenantSubscription? capturedSubscription = null;

        var subscriptionRepository = new Mock<ITenantSubscriptionRepository>();
        subscriptionRepository
            .Setup(x => x.GetByTenantIdAsync(TenantId, SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSubscription(subscriptionStatus));
        subscriptionRepository
            .Setup(x => x.UpdateAsync(It.IsAny<TenantSubscription>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()))
            .Callback<TenantSubscription, byte[]?, CancellationToken>((s, _, _) => capturedSubscription = s)
            .Returns(Task.CompletedTask);

        var tenantRepository = CreateTenantRepository(CreateTenant(tenantStatus), t => capturedTenant = t);
        var planRepository = CreatePlanRepository();
        var currentUser = CreateCurrentUser();

        var quotaService = new Mock<IQuotaService>();
        quotaService
            .Setup(x => x.InitializeTenantQuotasAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<IReadOnlyList<QuotaStatusDto>>.Success(new List<QuotaStatusDto>()));

        var handler = new ActivateTenantSubscriptionCommandHandler(
            subscriptionRepository.Object,
            tenantRepository.Object,
            planRepository.Object,
            currentUser.Object,
            quotaService.Object);

        return new ActivateFixture(handler, () => capturedTenant, () => capturedSubscription);
    }

    private static SuspendFixture CreateSuspendFixture(TenantStatus tenantStatus)
    {
        Tenant? capturedTenant = null;

        var subscriptionRepository = new Mock<ITenantSubscriptionRepository>();
        subscriptionRepository
            .Setup(x => x.GetByTenantIdAsync(TenantId, SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSubscription(TenantSubscriptionStatus.Active));
        subscriptionRepository
            .Setup(x => x.UpdateAsync(It.IsAny<TenantSubscription>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tenantRepository = CreateTenantRepository(CreateTenant(tenantStatus), t => capturedTenant = t);
        var planRepository = CreatePlanRepository();
        var currentUser = CreateCurrentUser();

        var eventBus = new Mock<IEventBus>();
        eventBus
            .Setup(x => x.PublishAsync(It.IsAny<TenantSubscriptionChangedV1>(), It.IsAny<EventPublishOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantSubscriptionChangedV1 @event, EventPublishOptions options, CancellationToken _) =>
                new EventEnvelope<TenantSubscriptionChangedV1>(
                    new EventMetadata(
                        options.EventId ?? Guid.NewGuid(),
                        @event.EventName,
                        @event.EventVersion,
                        options.CorrelationId ?? Guid.NewGuid(),
                        options.CausationId,
                        options.TenantId,
                        options.Producer ?? "Diten.Platform",
                        options.OccurredAtUtc ?? DateTimeOffset.UtcNow),
                    @event));

        var handler = new SuspendTenantSubscriptionCommandHandler(
            subscriptionRepository.Object,
            tenantRepository.Object,
            planRepository.Object,
            currentUser.Object,
            eventBus.Object);

        return new SuspendFixture(handler, () => capturedTenant);
    }

    private static Mock<ITenantRegistryRepository> CreateTenantRepository(Tenant tenant, Action<Tenant> capture)
    {
        var repository = new Mock<ITenantRegistryRepository>();
        repository
            .Setup(x => x.GetByIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        repository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .Callback<Tenant, CancellationToken>((t, _) => capture(t))
            .Returns(Task.CompletedTask);
        return repository;
    }

    private static Mock<ISubscriptionPlanRepository> CreatePlanRepository()
    {
        var repository = new Mock<ISubscriptionPlanRepository>();
        repository
            .Setup(x => x.GetByIdAsync(PlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan { Id = PlanId, Code = "PRO", Name = "Pro" });
        return repository;
    }

    private static Mock<ICurrentUserContext> CreateCurrentUser()
    {
        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));
        currentUser.SetupGet(x => x.ActorName).Returns("platform-admin");
        return currentUser;
    }

    private static TenantSubscription CreateSubscription(TenantSubscriptionStatus status) =>
        new()
        {
            Id = SubscriptionId,
            TenantId = TenantId,
            PlanId = PlanId,
            Status = status,
            CurrentPeriodStartUtc = DateTimeOffset.UtcNow.AddDays(-10),
            CurrentPeriodEndUtc = DateTimeOffset.UtcNow.AddDays(20),
            RowVersion = RowVersion
        };

    private static Tenant CreateTenant(TenantStatus status) =>
        new()
        {
            Id = TenantId,
            Code = "ACME",
            Slug = "acme",
            Name = "Acme",
            DisplayName = "Acme",
            Domain = "acme.local",
            PlanId = PlanId,
            PlanCode = "PRO",
            PlanName = "Pro",
            Status = status,
            ProvisioningStatus = "Started",
            SubscriptionStatus = TenantSubscriptionStatus.Trialing,
            ProvisioningSteps =
            [
                new TenantProvisioningStep { Key = "registry", Label = "Registry", Status = "Completed" },
                new TenantProvisioningStep { Key = "bootstrap-platform", Label = "Bootstrap", Status = "InProgress" },
                new TenantProvisioningStep { Key = "admin-invitation", Label = "Admin", Status = "Pending" }
            ]
        };

    private static ActivateTenantSubscriptionCommand CreateActivateCommand() =>
        new(TenantId, SubscriptionId, new ActivateTenantSubscriptionRequest(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(30),
            RowVersion));

    private static SuspendTenantSubscriptionCommand CreateSuspendCommand() =>
        new(TenantId, SubscriptionId, new SuspendTenantSubscriptionRequest("billing issue", RowVersion));

    private sealed record ActivateFixture(
        ActivateTenantSubscriptionCommandHandler Handler,
        Func<Tenant?> CapturedTenant,
        Func<TenantSubscription?> CapturedSubscription);

    private sealed record SuspendFixture(
        SuspendTenantSubscriptionCommandHandler Handler,
        Func<Tenant?> CapturedTenant);
}
