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

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class TenantModuleEntitlementMutationEventPublishTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid EntitlementId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly byte[] RowVersion = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd").ToByteArray();

    [Fact]
    public async Task EnableTenantModuleEntitlementCommandHandler_PublishesEnabledEventAfterSuccessfulMutation()
    {
        var fixture = CreateFixture(ActorId);
        var entitlement = CreateEntitlement(isEnabled: false);
        fixture.Repository.Setup(x => x.GetByIdAsync(TenantId, EntitlementId, It.IsAny<CancellationToken>())).ReturnsAsync(entitlement);
        TenantEntitlementEnabledV1? publishedEvent = null;
        EventPublishOptions? publishOptions = null;
        SetupPublish(fixture.EventBus, (TenantEntitlementEnabledV1 @event, EventPublishOptions options) =>
        {
            publishedEvent = @event;
            publishOptions = options;
        });
        var handler = new EnableTenantModuleEntitlementCommandHandler(
            fixture.Repository.Object,
            fixture.QuotaService.Object,
            fixture.EventBus.Object,
            fixture.CurrentUser.Object);

        var result = await handler.Handle(new EnableTenantModuleEntitlementCommand(TenantId, EntitlementId, RowVersion), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        AssertEntitlementEvent(publishedEvent, publishOptions, TenantEntitlementEnabledV1.Name, "HR", ActorId);
    }

    [Fact]
    public async Task EnableTenantModuleEntitlementCommandHandler_DoesNotPublishWhenAlreadyEnabled()
    {
        var fixture = CreateFixture(ActorId);
        fixture.Repository.Setup(x => x.GetByIdAsync(TenantId, EntitlementId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateEntitlement(isEnabled: true));
        var handler = new EnableTenantModuleEntitlementCommandHandler(
            fixture.Repository.Object,
            fixture.QuotaService.Object,
            fixture.EventBus.Object,
            fixture.CurrentUser.Object);

        var result = await handler.Handle(new EnableTenantModuleEntitlementCommand(TenantId, EntitlementId, RowVersion), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        VerifyPublishNever(fixture.EventBus);
    }

    [Fact]
    public async Task EnableTenantModuleEntitlementCommandHandler_DoesNotPublishWhenEntitlementMissing()
    {
        var fixture = CreateFixture(ActorId);
        fixture.Repository.Setup(x => x.GetByIdAsync(TenantId, EntitlementId, It.IsAny<CancellationToken>())).ReturnsAsync((TenantModuleEntitlement?)null);
        var handler = new EnableTenantModuleEntitlementCommandHandler(
            fixture.Repository.Object,
            fixture.QuotaService.Object,
            fixture.EventBus.Object,
            fixture.CurrentUser.Object);

        var result = await handler.Handle(new EnableTenantModuleEntitlementCommand(TenantId, EntitlementId, RowVersion), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        VerifyPublishNever(fixture.EventBus);
    }

    [Fact]
    public async Task DisableTenantModuleEntitlementCommandHandler_PublishesDisabledEventAfterSuccessfulPhysicalMutation()
    {
        var fixture = CreateFixture(ActorId);
        fixture.Repository.Setup(x => x.GetByIdAsync(TenantId, EntitlementId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateEntitlement(isEnabled: true));
        TenantEntitlementDisabledV1? publishedEvent = null;
        EventPublishOptions? publishOptions = null;
        SetupPublish(fixture.EventBus, (TenantEntitlementDisabledV1 @event, EventPublishOptions options) =>
        {
            publishedEvent = @event;
            publishOptions = options;
        });
        var handler = new DisableTenantModuleEntitlementCommandHandler(
            fixture.Repository.Object,
            fixture.ModuleRepository.Object,
            fixture.QuotaService.Object,
            fixture.EventBus.Object,
            fixture.CurrentUser.Object);

        var result = await handler.Handle(CreateDisableCommand(EntitlementId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        AssertEntitlementEvent(publishedEvent, publishOptions, TenantEntitlementDisabledV1.Name, "HR", ActorId);
    }

    [Fact]
    public async Task DisableTenantModuleEntitlementCommandHandler_DoesNotPublishWhenAlreadyDisabled()
    {
        var fixture = CreateFixture(ActorId);
        fixture.Repository.Setup(x => x.GetByIdAsync(TenantId, EntitlementId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateEntitlement(isEnabled: false));
        var handler = new DisableTenantModuleEntitlementCommandHandler(
            fixture.Repository.Object,
            fixture.ModuleRepository.Object,
            fixture.QuotaService.Object,
            fixture.EventBus.Object,
            fixture.CurrentUser.Object);

        var result = await handler.Handle(CreateDisableCommand(EntitlementId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        VerifyPublishNever(fixture.EventBus);
    }

    [Fact]
    public async Task DisableTenantModuleEntitlementCommandHandler_DoesNotPublishWhenModuleValidationFails()
    {
        var fixture = CreateFixture(ActorId);
        fixture.ModuleRepository.Setup(x => x.GetByCodeAsync("HR", It.IsAny<CancellationToken>())).ReturnsAsync((ModuleCatalogItem?)null);
        var handler = new DisableTenantModuleEntitlementCommandHandler(
            fixture.Repository.Object,
            fixture.ModuleRepository.Object,
            fixture.QuotaService.Object,
            fixture.EventBus.Object,
            fixture.CurrentUser.Object);

        var result = await handler.Handle(CreateDisableCommand(EntitlementId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        VerifyPublishNever(fixture.EventBus);
    }

    [Fact]
    public async Task UpdateTenantModuleEntitlementExpiryCommandHandler_PublishesExpiryUpdatedEventWhenExpiryChanges()
    {
        var fixture = CreateFixture(ActorId);
        fixture.Repository.Setup(x => x.GetByIdAsync(TenantId, EntitlementId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateEntitlement(isEnabled: true, expiryDateUtc: DateTimeOffset.UtcNow.AddDays(1)));
        TenantEntitlementExpiryUpdatedV1? publishedEvent = null;
        EventPublishOptions? publishOptions = null;
        SetupPublish(fixture.EventBus, (TenantEntitlementExpiryUpdatedV1 @event, EventPublishOptions options) =>
        {
            publishedEvent = @event;
            publishOptions = options;
        });
        var handler = new UpdateTenantModuleEntitlementExpiryCommandHandler(
            fixture.Repository.Object,
            fixture.EventBus.Object,
            fixture.CurrentUser.Object);

        var result = await handler.Handle(
            new UpdateTenantModuleEntitlementExpiryCommand(
                TenantId,
                EntitlementId,
                new UpdateTenantModuleEntitlementExpiryRequest(DateTimeOffset.UtcNow.AddDays(2), "extend", RowVersion)),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        AssertEntitlementEvent(publishedEvent, publishOptions, TenantEntitlementExpiryUpdatedV1.Name, "HR", ActorId);
    }

    [Fact]
    public async Task UpdateTenantModuleEntitlementExpiryCommandHandler_DoesNotPublishWhenExpiryUnchanged()
    {
        var fixture = CreateFixture(ActorId);
        var expiry = DateTimeOffset.UtcNow.AddDays(1);
        fixture.Repository.Setup(x => x.GetByIdAsync(TenantId, EntitlementId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateEntitlement(isEnabled: true, expiryDateUtc: expiry));
        var handler = new UpdateTenantModuleEntitlementExpiryCommandHandler(
            fixture.Repository.Object,
            fixture.EventBus.Object,
            fixture.CurrentUser.Object);

        var result = await handler.Handle(
            new UpdateTenantModuleEntitlementExpiryCommand(
                TenantId,
                EntitlementId,
                new UpdateTenantModuleEntitlementExpiryRequest(expiry, "reason only", RowVersion)),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        VerifyPublishNever(fixture.EventBus);
    }

    [Fact]
    public async Task UpdateTenantModuleEntitlementExpiryCommandHandler_DoesNotPublishWhenEntitlementMissing()
    {
        var fixture = CreateFixture(ActorId);
        fixture.Repository.Setup(x => x.GetByIdAsync(TenantId, EntitlementId, It.IsAny<CancellationToken>())).ReturnsAsync((TenantModuleEntitlement?)null);
        var handler = new UpdateTenantModuleEntitlementExpiryCommandHandler(
            fixture.Repository.Object,
            fixture.EventBus.Object,
            fixture.CurrentUser.Object);

        var result = await handler.Handle(
            new UpdateTenantModuleEntitlementExpiryCommand(
                TenantId,
                EntitlementId,
                new UpdateTenantModuleEntitlementExpiryRequest(DateTimeOffset.UtcNow.AddDays(2), "extend", RowVersion)),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        VerifyPublishNever(fixture.EventBus);
    }

    [Fact]
    public async Task RemoveTenantManualModuleOverrideCommandHandler_PublishesOverrideRemovedEventAfterSuccessfulRemoval()
    {
        var fixture = CreateFixture(Guid.Empty);
        fixture.Repository.Setup(x => x.GetByIdAsync(TenantId, EntitlementId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateEntitlement(isEnabled: false));
        TenantEntitlementOverrideRemovedV1? publishedEvent = null;
        EventPublishOptions? publishOptions = null;
        SetupPublish(fixture.EventBus, (TenantEntitlementOverrideRemovedV1 @event, EventPublishOptions options) =>
        {
            publishedEvent = @event;
            publishOptions = options;
        });
        var handler = new RemoveTenantManualModuleOverrideCommandHandler(
            fixture.Repository.Object,
            fixture.QuotaService.Object,
            fixture.EventBus.Object,
            fixture.CurrentUser.Object);

        var result = await handler.Handle(
            new RemoveTenantManualModuleOverrideCommand(
                TenantId,
                EntitlementId,
                new RemoveTenantManualModuleOverrideRequest(RowVersion)),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        AssertEntitlementEvent(publishedEvent, publishOptions, TenantEntitlementOverrideRemovedV1.Name, "HR", expectedActorId: null);
    }

    [Fact]
    public async Task RemoveTenantManualModuleOverrideCommandHandler_DoesNotPublishWhenEntitlementIsNotManualOverride()
    {
        var fixture = CreateFixture(ActorId);
        fixture.Repository.Setup(x => x.GetByIdAsync(TenantId, EntitlementId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateEntitlement(isEnabled: true, source: EntitlementSource.System));
        var handler = new RemoveTenantManualModuleOverrideCommandHandler(
            fixture.Repository.Object,
            fixture.QuotaService.Object,
            fixture.EventBus.Object,
            fixture.CurrentUser.Object);

        var result = await handler.Handle(
            new RemoveTenantManualModuleOverrideCommand(
                TenantId,
                EntitlementId,
                new RemoveTenantManualModuleOverrideRequest(RowVersion)),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        VerifyPublishNever(fixture.EventBus);
    }

    [Fact]
    public async Task RemoveTenantManualModuleOverrideCommandHandler_DoesNotPublishWhenEntitlementMissing()
    {
        var fixture = CreateFixture(ActorId);
        fixture.Repository.Setup(x => x.GetByIdAsync(TenantId, EntitlementId, It.IsAny<CancellationToken>())).ReturnsAsync((TenantModuleEntitlement?)null);
        var handler = new RemoveTenantManualModuleOverrideCommandHandler(
            fixture.Repository.Object,
            fixture.QuotaService.Object,
            fixture.EventBus.Object,
            fixture.CurrentUser.Object);

        var result = await handler.Handle(
            new RemoveTenantManualModuleOverrideCommand(
                TenantId,
                EntitlementId,
                new RemoveTenantManualModuleOverrideRequest(RowVersion)),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        VerifyPublishNever(fixture.EventBus);
    }

    private static TestFixture CreateFixture(Guid currentUserId)
    {
        var repository = new Mock<ITenantModuleEntitlementRepository>();
        repository
            .Setup(x => x.UpdateAsync(It.IsAny<TenantModuleEntitlement>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository
            .Setup(x => x.SoftDeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository
            .Setup(x => x.CreateAsync(It.IsAny<TenantModuleEntitlement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantModuleEntitlement entitlement, CancellationToken _) => entitlement);

        var moduleRepository = new Mock<IModuleCatalogRepository>();
        moduleRepository
            .Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string moduleCode, CancellationToken _) => new ModuleCatalogItem
            {
                ModuleCode = moduleCode,
                ModuleName = moduleCode,
                DisplayName = moduleCode,
                Status = ModuleCatalogStatus.Active
            });

        var quotaService = new Mock<IQuotaService>();
        quotaService
            .Setup(x => x.TryConsumeAsync(It.IsAny<TryConsumeQuotaRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<QuotaMutationDto>.Success(CreateQuotaMutation()));
        quotaService
            .Setup(x => x.ReleaseAsync(It.IsAny<ReleaseQuotaRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<QuotaMutationDto>.Success(CreateQuotaMutation()));

        var eventBus = new Mock<IEventBus>();
        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(x => x.UserId).Returns(currentUserId);

        return new TestFixture(repository, moduleRepository, quotaService, eventBus, currentUser);
    }

    private static TenantModuleEntitlement CreateEntitlement(
        bool isEnabled,
        DateTimeOffset? expiryDateUtc = null,
        EntitlementSource source = EntitlementSource.ManualOverride)
    {
        return new TenantModuleEntitlement
        {
            Id = EntitlementId,
            TenantId = TenantId,
            ModuleCode = "HR",
            Source = source,
            IsEnabled = isEnabled,
            ExpiryDateUtc = expiryDateUtc,
            RowVersion = RowVersion
        };
    }

    private static DisableTenantModuleEntitlementCommand CreateDisableCommand(Guid entitlementId)
    {
        return new DisableTenantModuleEntitlementCommand(
            TenantId,
            new DisableTenantModuleEntitlementRequest("HR", entitlementId, "disable", RowVersion));
    }

    private static QuotaMutationDto CreateQuotaMutation()
    {
        return new QuotaMutationDto(TenantId, "modules.max", 1, 10, 1, Applied: true, ErrorCode: null);
    }

    private static void SetupPublish<TEvent>(
        Mock<IEventBus> eventBus,
        Action<TEvent, EventPublishOptions> capture)
        where TEvent : IIntegrationEvent
    {
        eventBus
            .Setup(x => x.PublishAsync(
                It.IsAny<TEvent>(),
                It.IsAny<EventPublishOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<TEvent, EventPublishOptions, CancellationToken>((@event, options, _) => capture(@event, options))
            .ReturnsAsync((TEvent @event, EventPublishOptions options, CancellationToken _) => CreateEnvelope(@event, options));
    }

    private static EventEnvelope<TEvent> CreateEnvelope<TEvent>(TEvent @event, EventPublishOptions options)
        where TEvent : IIntegrationEvent
    {
        return new EventEnvelope<TEvent>(
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

    private static void AssertEntitlementEvent<TEvent>(
        TEvent? publishedEvent,
        EventPublishOptions? publishOptions,
        string expectedEventName,
        string expectedModuleCode,
        Guid? expectedActorId)
        where TEvent : IIntegrationEvent
    {
        Assert.NotNull(publishedEvent);
        Assert.NotNull(publishOptions);

        var eventType = publishedEvent.GetType();
        Assert.Equal(expectedEventName, publishedEvent.EventName);
        Assert.Equal(TenantId, eventType.GetProperty("TenantId")!.GetValue(publishedEvent));
        Assert.Equal(expectedModuleCode, eventType.GetProperty("ModuleCode")!.GetValue(publishedEvent));
        Assert.Equal(expectedActorId, eventType.GetProperty("ActorId")!.GetValue(publishedEvent));
        Assert.Equal(eventType.GetProperty("EventId")!.GetValue(publishedEvent), publishOptions.EventId);
        Assert.Equal(eventType.GetProperty("CorrelationId")!.GetValue(publishedEvent), publishOptions.CorrelationId);
        Assert.Equal(eventType.GetProperty("OccurredAtUtc")!.GetValue(publishedEvent), publishOptions.OccurredAtUtc);
        Assert.Equal(TenantId, publishOptions.TenantId);
    }

    private static void VerifyPublishNever(Mock<IEventBus> eventBus)
    {
        Assert.DoesNotContain(
            eventBus.Invocations,
            invocation => invocation.Method.Name == nameof(IEventBus.PublishAsync));
    }

    private sealed record TestFixture(
        Mock<ITenantModuleEntitlementRepository> Repository,
        Mock<IModuleCatalogRepository> ModuleRepository,
        Mock<IQuotaService> QuotaService,
        Mock<IEventBus> EventBus,
        Mock<ICurrentUserContext> CurrentUser);
}
