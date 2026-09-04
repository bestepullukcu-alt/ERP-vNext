using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts;
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

public sealed class SuspendTenantSubscriptionCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SubscriptionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PlanId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid ActorId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly byte[] RowVersion = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee").ToByteArray();

    [Fact]
    public async Task SuspendTenantSubscriptionCommandHandler_PublishesSubscriptionChangedAfterSnapshotSuccess()
    {
        var fixture = CreateFixture(ActorId);
        TenantSubscriptionChangedV1? publishedEvent = null;
        EventPublishOptions? publishOptions = null;
        var result = await fixture.Handler.Handle(CreateCommand(), CancellationToken.None);
        publishedEvent = (TenantSubscriptionChangedV1?)fixture.Transactions.Events.Event;
        publishOptions = fixture.Transactions.Events.Options;

        Assert.True(result.IsSuccessful);
        Assert.NotNull(publishedEvent);
        Assert.NotNull(publishOptions);
        Assert.Equal(TenantSubscriptionChangedV1.Name, publishedEvent.EventName);
        Assert.Equal(TenantId, publishedEvent.TenantId);
        Assert.Equal(PlanId, publishedEvent.PreviousPlanId);
        Assert.Equal(PlanId, publishedEvent.NewPlanId);
        Assert.Equal(TenantSubscriptionStatus.Active.ToString(), publishedEvent.PreviousStatus);
        Assert.Equal(TenantSubscriptionStatus.Suspended.ToString(), publishedEvent.NewStatus);
        Assert.Equal(ActorId, publishedEvent.ActorId);
        Assert.Equal(publishedEvent.EventId, publishOptions.EventId);
        Assert.Equal(publishedEvent.CorrelationId, publishOptions.CorrelationId);
        Assert.Equal(publishedEvent.OccurredAtUtc, publishOptions.OccurredAtUtc);
        Assert.Equal(TenantId, publishOptions.TenantId);
    }

    [Fact]
    public async Task SuspendTenantSubscriptionCommandHandler_UsesNullActorIdWhenCurrentUserIdIsEmpty()
    {
        var fixture = CreateFixture(Guid.Empty);
        TenantSubscriptionChangedV1? publishedEvent = null;
        var result = await fixture.Handler.Handle(CreateCommand(), CancellationToken.None);
        publishedEvent = (TenantSubscriptionChangedV1?)fixture.Transactions.Events.Event;

        Assert.True(result.IsSuccessful);
        Assert.NotNull(publishedEvent);
        Assert.Null(publishedEvent.ActorId);
    }

    [Fact]
    public async Task SuspendTenantSubscriptionCommandHandler_DoesNotPublishWhenSubscriptionMissing()
    {
        var fixture = CreateFixture(ActorId);
        fixture.SubscriptionRepository
            .Setup(x => x.GetByTenantIdAsync(TenantId, SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantSubscription?)null);

        var result = await fixture.Handler.Handle(CreateCommand(), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(0, fixture.Transactions.Events.Count);
    }

    [Fact]
    public async Task SuspendTenantSubscriptionCommandHandler_DoesNotPublishWhenTransitionInvalid()
    {
        var fixture = CreateFixture(ActorId, TenantSubscriptionStatus.Suspended);

        var result = await fixture.Handler.Handle(CreateCommand(), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(0, fixture.Transactions.Events.Count);
    }

    [Fact]
    public async Task SuspendTenantSubscriptionCommandHandler_DoesNotPublishWhenConcurrencyFails()
    {
        var fixture = CreateFixture(ActorId);
        fixture.SubscriptionRepository
            .Setup(x => x.UpdateAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<TenantSubscription>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TenantSubscriptionConcurrencyException());

        var result = await fixture.Handler.Handle(CreateCommand(), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(0, fixture.Transactions.Events.Count);
    }

    [Fact]
    public async Task SuspendTenantSubscriptionCommandHandler_DoesNotPublishWhenSnapshotUpdateFails()
    {
        var fixture = CreateFixture(ActorId);
        fixture.TenantRepository
            .Setup(x => x.GetByIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        var result = await fixture.Handler.Handle(CreateCommand(), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(0, fixture.Transactions.Events.Count);
    }

    private static TestFixture CreateFixture(
        Guid currentUserId,
        TenantSubscriptionStatus subscriptionStatus = TenantSubscriptionStatus.Active)
    {
        var subscriptionRepository = new Mock<ITenantSubscriptionRepository>();
        subscriptionRepository
            .Setup(x => x.GetByTenantIdAsync(TenantId, SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSubscription(subscriptionStatus));
        subscriptionRepository
            .Setup(x => x.UpdateAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<TenantSubscription>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tenantRepository = new Mock<ITenantRegistryRepository>();
        tenantRepository
            .Setup(x => x.GetByIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant());
        tenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var planRepository = new Mock<ISubscriptionPlanRepository>();
        planRepository
            .Setup(x => x.GetByIdAsync(PlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan { Id = PlanId, Code = "PRO", Name = "Pro" });

        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(x => x.UserId).Returns(currentUserId);
        currentUser.SetupGet(x => x.ActorName).Returns("platform-admin");

        var transactions = new SubscriptionTransactionTestDependencies(subscriptionRepository.Object,
            tenantRepository.Object, planRepository.Object, currentUser.Object);
        var handler = new SuspendTenantSubscriptionCommandHandler(
            subscriptionRepository.Object,
            tenantRepository.Object,
            planRepository.Object,
            currentUser.Object,
            transactions.Writer);

        return new TestFixture(handler, subscriptionRepository, tenantRepository, transactions);
    }

    private static TenantSubscription CreateSubscription(TenantSubscriptionStatus status)
    {
        return new TenantSubscription
        {
            Id = SubscriptionId,
            TenantId = TenantId,
            PlanId = PlanId,
            Status = status,
            CurrentPeriodStartUtc = DateTimeOffset.UtcNow.AddDays(-10),
            CurrentPeriodEndUtc = DateTimeOffset.UtcNow.AddDays(20),
            RowVersion = RowVersion
        };
    }

    private static Tenant CreateTenant()
    {
        return new Tenant
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
            SubscriptionStatus = TenantSubscriptionStatus.Active
        };
    }

    private static SuspendTenantSubscriptionCommand CreateCommand()
    {
        return new SuspendTenantSubscriptionCommand(
            TenantId,
            SubscriptionId,
            new SuspendTenantSubscriptionRequest("billing issue", RowVersion));
    }

    private sealed record TestFixture(
        SuspendTenantSubscriptionCommandHandler Handler,
        Mock<ITenantSubscriptionRepository> SubscriptionRepository,
        Mock<ITenantRegistryRepository> TenantRepository,
        SubscriptionTransactionTestDependencies Transactions);
}
