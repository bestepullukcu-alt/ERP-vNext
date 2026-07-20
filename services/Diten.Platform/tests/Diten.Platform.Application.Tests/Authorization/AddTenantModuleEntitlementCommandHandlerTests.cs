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

public sealed class AddTenantModuleEntitlementCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task AddTenantModuleEntitlementCommandHandler_PublishesTenantEntitlementAddedAfterSuccessfulCreate()
    {
        var fixture = CreateFixture(ActorId);
        TenantEntitlementAddedV1? publishedEvent = null;
        EventPublishOptions? publishOptions = null;
        fixture.EventBus
            .Setup(x => x.PublishAsync(
                It.IsAny<TenantEntitlementAddedV1>(),
                It.IsAny<EventPublishOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<TenantEntitlementAddedV1, EventPublishOptions, CancellationToken>((@event, options, _) =>
            {
                publishedEvent = @event;
                publishOptions = options;
            })
            .ReturnsAsync((TenantEntitlementAddedV1 @event, EventPublishOptions options, CancellationToken _) =>
                CreateEnvelope(@event, options));

        var result = await fixture.Handler.Handle(CreateCommand(" hr "), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(publishedEvent);
        Assert.NotNull(publishOptions);
        Assert.Equal(TenantEntitlementAddedV1.Name, publishedEvent.EventName);
        Assert.Equal(TenantId, publishedEvent.TenantId);
        Assert.Equal("HR", publishedEvent.ModuleCode);
        Assert.Equal(ActorId, publishedEvent.ActorId);
        Assert.Equal(publishedEvent.EventId, publishOptions.EventId);
        Assert.Equal(publishedEvent.CorrelationId, publishOptions.CorrelationId);
        Assert.Equal(publishedEvent.OccurredAtUtc, publishOptions.OccurredAtUtc);
        Assert.Equal(TenantId, publishOptions.TenantId);
        fixture.EventBus.Verify(
            x => x.PublishAsync(
                It.IsAny<TenantEntitlementAddedV1>(),
                It.IsAny<EventPublishOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddTenantModuleEntitlementCommandHandler_UsesNullActorIdWhenCurrentUserIdIsEmpty()
    {
        var fixture = CreateFixture(Guid.Empty);
        TenantEntitlementAddedV1? publishedEvent = null;
        fixture.EventBus
            .Setup(x => x.PublishAsync(
                It.IsAny<TenantEntitlementAddedV1>(),
                It.IsAny<EventPublishOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<TenantEntitlementAddedV1, EventPublishOptions, CancellationToken>((@event, _, _) => publishedEvent = @event)
            .ReturnsAsync((TenantEntitlementAddedV1 @event, EventPublishOptions options, CancellationToken _) =>
                CreateEnvelope(@event, options));

        await fixture.Handler.Handle(CreateCommand("HR"), CancellationToken.None);

        Assert.NotNull(publishedEvent);
        Assert.Null(publishedEvent.ActorId);
    }

    [Fact]
    public async Task AddTenantModuleEntitlementCommandHandler_DoesNotPublishWhenModuleValidationFails()
    {
        var fixture = CreateFixture(ActorId);
        fixture.ModuleRepository
            .Setup(x => x.GetByCodeAsync("HR", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModuleCatalogItem?)null);

        var result = await fixture.Handler.Handle(CreateCommand("HR"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        VerifyPublishNever(fixture.EventBus);
        fixture.Repository.Verify(
            x => x.CreateAsync(It.IsAny<TenantModuleEntitlement>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // FEAT-BASELINE-MODULES — the write path rejects a baseline module (409): baseline modules are entitlement-free
    // and must never receive a manual entitlement row, even via a direct API call. Guard keys off IsBaseline.
    [Fact]
    public async Task AddTenantModuleEntitlementCommandHandler_RejectsBaselineModule()
    {
        var fixture = CreateFixture(ActorId);
        fixture.ModuleRepository
            .Setup(x => x.GetByCodeAsync("HR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleCatalogItem
            {
                ModuleCode = "HR",
                ModuleName = "HR",
                DisplayName = "HR",
                Status = ModuleCatalogStatus.Active,
                IsBaseline = true
            });

        var result = await fixture.Handler.Handle(CreateCommand("HR"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        VerifyPublishNever(fixture.EventBus);
        fixture.Repository.Verify(
            x => x.CreateAsync(It.IsAny<TenantModuleEntitlement>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // FIX-ENTITLEMENT-DUP — an active row under ANY source blocks a re-add, regardless of the incoming Source.
    [Fact]
    public async Task AddTenantModuleEntitlementCommandHandler_DoesNotPublishWhenDuplicateExists()
    {
        var fixture = CreateFixture(ActorId);
        fixture.Repository
            .Setup(x => x.GetByTenantAndModuleAsync(TenantId, "HR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantModuleEntitlement>
            {
                // Different source than the incoming ManualOverride — still a duplicate (module-based guard).
                new() { TenantId = TenantId, ModuleCode = "HR", Source = EntitlementSource.System, IsEnabled = true }
            });

        var result = await fixture.Handler.Handle(CreateCommand("HR"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        VerifyPublishNever(fixture.EventBus);
        fixture.Repository.Verify(
            x => x.CreateAsync(It.IsAny<TenantModuleEntitlement>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // A disabled (IsEnabled=false) row must NOT block re-adding — the re-enable / re-add path stays open.
    [Fact]
    public async Task AddTenantModuleEntitlementCommandHandler_AllowsReAddWhenExistingRowIsDisabled()
    {
        var fixture = CreateFixture(ActorId);
        fixture.Repository
            .Setup(x => x.GetByTenantAndModuleAsync(TenantId, "HR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantModuleEntitlement>
            {
                new() { TenantId = TenantId, ModuleCode = "HR", Source = EntitlementSource.ManualOverride, IsEnabled = false }
            });

        var result = await fixture.Handler.Handle(CreateCommand("HR"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        fixture.Repository.Verify(
            x => x.CreateAsync(It.IsAny<TenantModuleEntitlement>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddTenantModuleEntitlementCommandHandler_DoesNotPublishWhenQuotaConsumeFails()
    {
        var fixture = CreateFixture(ActorId);
        fixture.QuotaService
            .Setup(x => x.TryConsumeAsync(It.IsAny<TryConsumeQuotaRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<QuotaMutationDto>.Fail("quota exceeded", 409));

        var result = await fixture.Handler.Handle(CreateCommand("HR"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        VerifyPublishNever(fixture.EventBus);
        fixture.Repository.Verify(
            x => x.CreateAsync(It.IsAny<TenantModuleEntitlement>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static TestFixture CreateFixture(Guid currentUserId)
    {
        var repository = new Mock<ITenantModuleEntitlementRepository>();
        repository
            .Setup(x => x.GetByTenantAndModuleAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantModuleEntitlement>());
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
            .ReturnsAsync(Response<QuotaMutationDto>.Success(new QuotaMutationDto(
                TenantId,
                "modules.max",
                1,
                10,
                1,
                Applied: true,
                ErrorCode: null)));

        var eventBus = new Mock<IEventBus>();
        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(x => x.UserId).Returns(currentUserId);

        var handler = new AddTenantModuleEntitlementCommandHandler(
            repository.Object,
            moduleRepository.Object,
            quotaService.Object,
            eventBus.Object,
            currentUser.Object);

        return new TestFixture(handler, repository, moduleRepository, quotaService, eventBus);
    }

    private static AddTenantModuleEntitlementCommand CreateCommand(string moduleCode)
    {
        return new AddTenantModuleEntitlementCommand(
            TenantId,
            new TenantModuleEntitlementRequest(
                moduleCode,
                EntitlementSource.ManualOverride,
                IsEnabled: true,
                ExpiryDateUtc: null,
                Reason: "test",
                RowVersion: null));
    }

    private static EventEnvelope<TenantEntitlementAddedV1> CreateEnvelope(
        TenantEntitlementAddedV1 @event,
        EventPublishOptions options)
    {
        return new EventEnvelope<TenantEntitlementAddedV1>(
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

    private static void VerifyPublishNever(Mock<IEventBus> eventBus)
    {
        eventBus.Verify(
            x => x.PublishAsync(
                It.IsAny<TenantEntitlementAddedV1>(),
                It.IsAny<EventPublishOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed record TestFixture(
        AddTenantModuleEntitlementCommandHandler Handler,
        Mock<ITenantModuleEntitlementRepository> Repository,
        Mock<IModuleCatalogRepository> ModuleRepository,
        Mock<IQuotaService> QuotaService,
        Mock<IEventBus> EventBus);
}
