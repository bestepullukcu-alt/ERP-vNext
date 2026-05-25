using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
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

public sealed class TenantSubscriptionChangedPublishHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SubscriptionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PlanId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid ActorId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly byte[] RowVersion = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee").ToByteArray();

    public static TheoryData<HandlerKind, TenantSubscriptionStatus, TenantSubscriptionStatus> SuccessfulTransitions =>
        new()
        {
            { HandlerKind.Renew, TenantSubscriptionStatus.Active, TenantSubscriptionStatus.Active },
            { HandlerKind.Cancel, TenantSubscriptionStatus.Active, TenantSubscriptionStatus.Cancelled },
            { HandlerKind.Expire, TenantSubscriptionStatus.Active, TenantSubscriptionStatus.Expired },
            { HandlerKind.Reactivate, TenantSubscriptionStatus.Suspended, TenantSubscriptionStatus.Active }
        };

    public static TheoryData<HandlerKind> HandlerKinds =>
        new()
        {
            HandlerKind.Renew,
            HandlerKind.Cancel,
            HandlerKind.Expire,
            HandlerKind.Reactivate
        };

    [Theory]
    [MemberData(nameof(SuccessfulTransitions))]
    public async Task TenantSubscriptionMutationHandler_PublishesSubscriptionChangedAfterSnapshotSuccess(
        HandlerKind kind,
        TenantSubscriptionStatus previousStatus,
        TenantSubscriptionStatus newStatus)
    {
        var fixture = CreateFixture(ActorId, previousStatus);
        TenantSubscriptionChangedV1? publishedEvent = null;
        EventPublishOptions? publishOptions = null;
        SetupPublish(fixture.EventBus, (@event, options) =>
        {
            publishedEvent = @event;
            publishOptions = options;
        });

        var result = await ExecuteAsync(kind, fixture);

        Assert.True(result.IsSuccessful);
        AssertSubscriptionChangedEvent(publishedEvent, publishOptions, previousStatus, newStatus, ActorId);
    }

    [Fact]
    public async Task CancelTenantSubscriptionCommandHandler_PublishesWhenCancelAtPeriodEndKeepsStatusActive()
    {
        var fixture = CreateFixture(ActorId, TenantSubscriptionStatus.Active);
        TenantSubscriptionChangedV1? publishedEvent = null;
        EventPublishOptions? publishOptions = null;
        SetupPublish(fixture.EventBus, (@event, options) =>
        {
            publishedEvent = @event;
            publishOptions = options;
        });

        var result = await ExecuteCancelAsync(fixture, cancelAtPeriodEnd: true);

        Assert.True(result.IsSuccessful);
        AssertSubscriptionChangedEvent(
            publishedEvent,
            publishOptions,
            TenantSubscriptionStatus.Active,
            TenantSubscriptionStatus.Active,
            ActorId);
    }

    [Fact]
    public async Task TenantSubscriptionMutationHandler_UsesNullActorIdWhenCurrentUserIdIsEmpty()
    {
        var fixture = CreateFixture(Guid.Empty, TenantSubscriptionStatus.Suspended);
        TenantSubscriptionChangedV1? publishedEvent = null;
        EventPublishOptions? publishOptions = null;
        SetupPublish(fixture.EventBus, (@event, options) =>
        {
            publishedEvent = @event;
            publishOptions = options;
        });

        var result = await ExecuteAsync(HandlerKind.Reactivate, fixture);

        Assert.True(result.IsSuccessful);
        AssertSubscriptionChangedEvent(
            publishedEvent,
            publishOptions,
            TenantSubscriptionStatus.Suspended,
            TenantSubscriptionStatus.Active,
            expectedActorId: null);
    }

    [Theory]
    [MemberData(nameof(HandlerKinds))]
    public async Task TenantSubscriptionMutationHandler_DoesNotPublishWhenSubscriptionMissing(HandlerKind kind)
    {
        var fixture = CreateFixture(ActorId, ValidStatusFor(kind));
        fixture.SubscriptionRepository
            .Setup(x => x.GetByTenantIdAsync(TenantId, SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantSubscription?)null);

        var result = await ExecuteAsync(kind, fixture);

        Assert.False(result.IsSuccessful);
        VerifyPublishNever(fixture.EventBus);
    }

    [Theory]
    [MemberData(nameof(HandlerKinds))]
    public async Task TenantSubscriptionMutationHandler_DoesNotPublishWhenTransitionInvalid(HandlerKind kind)
    {
        var fixture = CreateFixture(ActorId, InvalidStatusFor(kind));

        var result = await ExecuteAsync(kind, fixture);

        Assert.False(result.IsSuccessful);
        VerifyPublishNever(fixture.EventBus);
    }

    [Theory]
    [MemberData(nameof(HandlerKinds))]
    public async Task TenantSubscriptionMutationHandler_DoesNotPublishWhenSnapshotUpdateFails(HandlerKind kind)
    {
        var fixture = CreateFixture(ActorId, ValidStatusFor(kind));
        fixture.TenantRepository
            .Setup(x => x.GetByIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        var result = await ExecuteAsync(kind, fixture);

        Assert.False(result.IsSuccessful);
        VerifyPublishNever(fixture.EventBus);
    }

    private static TestFixture CreateFixture(Guid currentUserId, TenantSubscriptionStatus subscriptionStatus)
    {
        var subscriptionRepository = new Mock<ITenantSubscriptionRepository>();
        subscriptionRepository
            .Setup(x => x.GetByTenantIdAsync(TenantId, SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSubscription(subscriptionStatus));
        subscriptionRepository
            .Setup(x => x.UpdateAsync(It.IsAny<TenantSubscription>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tenantRepository = new Mock<ITenantRegistryRepository>();
        tenantRepository
            .Setup(x => x.GetByIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant());
        tenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var planRepository = new Mock<ISubscriptionPlanRepository>();
        planRepository
            .Setup(x => x.GetByIdAsync(PlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan { Id = PlanId, Code = "PRO", Name = "Pro" });

        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(x => x.UserId).Returns(currentUserId);
        currentUser.SetupGet(x => x.ActorName).Returns("platform-admin");

        return new TestFixture(
            subscriptionRepository,
            tenantRepository,
            planRepository,
            currentUser,
            new Mock<IEventBus>());
    }

    private static Task<Response<NoContent>> ExecuteAsync(HandlerKind kind, TestFixture fixture)
    {
        return kind switch
        {
            HandlerKind.Renew => new RenewTenantSubscriptionCommandHandler(
                fixture.SubscriptionRepository.Object,
                fixture.TenantRepository.Object,
                fixture.PlanRepository.Object,
                fixture.CurrentUser.Object,
                fixture.EventBus.Object).Handle(
                    new RenewTenantSubscriptionCommand(
                        TenantId,
                        SubscriptionId,
                        new RenewTenantSubscriptionRequest(DateTimeOffset.UtcNow.AddDays(40), RowVersion)),
                    CancellationToken.None),

            HandlerKind.Cancel => ExecuteCancelAsync(fixture, cancelAtPeriodEnd: false),

            HandlerKind.Expire => new ExpireTenantSubscriptionCommandHandler(
                fixture.SubscriptionRepository.Object,
                fixture.TenantRepository.Object,
                fixture.PlanRepository.Object,
                fixture.CurrentUser.Object,
                fixture.EventBus.Object).Handle(
                    new ExpireTenantSubscriptionCommand(TenantId, SubscriptionId, RowVersion),
                    CancellationToken.None),

            HandlerKind.Reactivate => new ReactivateTenantSubscriptionCommandHandler(
                fixture.SubscriptionRepository.Object,
                fixture.TenantRepository.Object,
                fixture.PlanRepository.Object,
                fixture.CurrentUser.Object,
                fixture.EventBus.Object).Handle(
                    new ReactivateTenantSubscriptionCommand(
                        TenantId,
                        SubscriptionId,
                        new ReactivateTenantSubscriptionRequest("resolved", RowVersion)),
                    CancellationToken.None),

            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static Task<Response<NoContent>> ExecuteCancelAsync(TestFixture fixture, bool cancelAtPeriodEnd)
    {
        return new CancelTenantSubscriptionCommandHandler(
            fixture.SubscriptionRepository.Object,
            fixture.TenantRepository.Object,
            fixture.PlanRepository.Object,
            fixture.CurrentUser.Object,
            fixture.EventBus.Object).Handle(
                new CancelTenantSubscriptionCommand(
                    TenantId,
                    SubscriptionId,
                    new CancelTenantSubscriptionRequest("customer request", cancelAtPeriodEnd, RowVersion)),
                CancellationToken.None);
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

    private static TenantSubscriptionStatus ValidStatusFor(HandlerKind kind) =>
        kind == HandlerKind.Reactivate ? TenantSubscriptionStatus.Suspended : TenantSubscriptionStatus.Active;

    private static TenantSubscriptionStatus InvalidStatusFor(HandlerKind kind) =>
        kind == HandlerKind.Reactivate ? TenantSubscriptionStatus.Active : TenantSubscriptionStatus.Cancelled;

    private static void SetupPublish(
        Mock<IEventBus> eventBus,
        Action<TenantSubscriptionChangedV1, EventPublishOptions> capture)
    {
        eventBus
            .Setup(x => x.PublishAsync(
                It.IsAny<TenantSubscriptionChangedV1>(),
                It.IsAny<EventPublishOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<TenantSubscriptionChangedV1, EventPublishOptions, CancellationToken>((@event, options, _) => capture(@event, options))
            .ReturnsAsync((TenantSubscriptionChangedV1 @event, EventPublishOptions options, CancellationToken _) =>
                CreateEnvelope(@event, options));
    }

    private static EventEnvelope<TenantSubscriptionChangedV1> CreateEnvelope(
        TenantSubscriptionChangedV1 @event,
        EventPublishOptions options)
    {
        return new EventEnvelope<TenantSubscriptionChangedV1>(
            new EventMetadata(
                options.EventId ?? Guid.NewGuid(),
                @event.EventName,
                @event.EventVersion,
                options.CorrelationId ?? Guid.NewGuid(),
                options.CausationId,
                options.TenantId,
                options.Producer ?? "Diten.Platform",
                options.OccurredAtUtc ?? DateTimeOffset.UtcNow),
            @event);
    }

    private static void AssertSubscriptionChangedEvent(
        TenantSubscriptionChangedV1? publishedEvent,
        EventPublishOptions? publishOptions,
        TenantSubscriptionStatus expectedPreviousStatus,
        TenantSubscriptionStatus expectedNewStatus,
        Guid? expectedActorId)
    {
        Assert.NotNull(publishedEvent);
        Assert.NotNull(publishOptions);
        Assert.Equal(TenantSubscriptionChangedV1.Name, publishedEvent.EventName);
        Assert.Equal(TenantId, publishedEvent.TenantId);
        Assert.Equal(PlanId, publishedEvent.PreviousPlanId);
        Assert.Equal(PlanId, publishedEvent.NewPlanId);
        Assert.Equal(expectedPreviousStatus.ToString(), publishedEvent.PreviousStatus);
        Assert.Equal(expectedNewStatus.ToString(), publishedEvent.NewStatus);
        Assert.Equal(expectedActorId, publishedEvent.ActorId);
        Assert.Equal(publishedEvent.EventId, publishOptions.EventId);
        Assert.Equal(publishedEvent.CorrelationId, publishOptions.CorrelationId);
        Assert.Equal(publishedEvent.OccurredAtUtc, publishOptions.OccurredAtUtc);
        Assert.Equal(TenantId, publishOptions.TenantId);
    }

    private static void VerifyPublishNever(Mock<IEventBus> eventBus)
    {
        Assert.DoesNotContain(
            eventBus.Invocations,
            invocation => invocation.Method.Name == nameof(IEventBus.PublishAsync));
    }

    public enum HandlerKind
    {
        Renew,
        Cancel,
        Expire,
        Reactivate
    }

    private sealed record TestFixture(
        Mock<ITenantSubscriptionRepository> SubscriptionRepository,
        Mock<ITenantRegistryRepository> TenantRepository,
        Mock<ISubscriptionPlanRepository> PlanRepository,
        Mock<ICurrentUserContext> CurrentUser,
        Mock<IEventBus> EventBus);
}
