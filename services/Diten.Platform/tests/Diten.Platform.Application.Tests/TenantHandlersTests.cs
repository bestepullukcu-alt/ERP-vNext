using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Application.Features.Tenants.Handlers;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests;

public sealed class TenantHandlersTests
{
    private readonly Mock<ITenantRegistryRepository> _repository;
    private readonly Mock<ISubscriptionPlanRepository> _subscriptionPlanRepository;
    private readonly Mock<ITenantSubscriptionRepository> _tenantSubscriptionRepository;
    private readonly Mock<ITenantDomainRepository> _domainRepository;
    private readonly Mock<ITenantLoginSettingsRepository> _loginSettingsRepository;
    private readonly Mock<ITenantDefaultsProvider> _defaults;
    private readonly Mock<ICurrentUserContext> _currentUser;
    private readonly Mock<IEventBus> _eventBus;
    private readonly Mock<IQuotaService> _quotaService;
    private readonly Mock<ITenantActivationNotifier> _tenantActivationNotifier;
    private readonly Mock<ILogger<RegisterTenantCommandHandler>> _logger;
    private readonly RegisterTenantCommandHandler _handler;

    public TenantHandlersTests()
    {
        _repository = new Mock<ITenantRegistryRepository>();
        _repository.Setup(x => x.GetByDomainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);
        _repository.Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);
        _repository.Setup(x => x.GetBySlugAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);
        Tenant? createdTenant = null;
        _repository.Setup(x => x.CreateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) =>
            {
                createdTenant = t;
                return t;
            });
        // Mirror Mongo: each read returns a FRESH copy, so post-create updates (subscription snapshot /
        // provisioning finalize) never mutate the instance CreateAsync captured for the It.Is<> verifies.
        _repository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => createdTenant?.Id == id ? CloneForRead(createdTenant) : null);

        _domainRepository = new Mock<ITenantDomainRepository>();
        _domainRepository.Setup(x => x.GetByDomainNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantDomain?)null);
        _domainRepository.Setup(x => x.CreateAsync(It.IsAny<TenantDomain>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantDomain d, CancellationToken _) => d);

        _subscriptionPlanRepository = new Mock<ISubscriptionPlanRepository>();
        _subscriptionPlanRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new SubscriptionPlan
            {
                Id = id,
                Code = "FREE",
                Name = "Free",
                IsActive = true,
                IsTrialPlan = true,
                TrialDurationDays = 14
            });

        _tenantSubscriptionRepository = new Mock<ITenantSubscriptionRepository>();
        _tenantSubscriptionRepository.Setup(x => x.HasCurrentAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _tenantSubscriptionRepository.Setup(x => x.CreateAsync(It.IsAny<TenantSubscription>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantSubscription subscription, CancellationToken _) => subscription);

        _loginSettingsRepository = new Mock<ITenantLoginSettingsRepository>();
        _loginSettingsRepository.Setup(x => x.GetByTenantRefIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantLoginSettings?)null);
        _loginSettingsRepository.Setup(x => x.CreateAsync(It.IsAny<TenantLoginSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantLoginSettings s, CancellationToken _) => s);
        _loginSettingsRepository.Setup(x => x.UpdateAsync(It.IsAny<TenantLoginSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _defaults = new Mock<ITenantDefaultsProvider>();
        _defaults.SetupGet(x => x.DefaultRegion).Returns("US");
        _defaults.SetupGet(x => x.DefaultEnvironment).Returns("Production");
        _defaults.SetupGet(x => x.DefaultTier).Returns("Standard");
        _defaults.SetupGet(x => x.DefaultLanguage).Returns("en");
        _defaults.SetupGet(x => x.DefaultTimezone).Returns("UTC");
        _defaults.SetupGet(x => x.DefaultCurrency).Returns("USD");
        _defaults.SetupGet(x => x.AppUrlTemplate).Returns("https://{tenant}.diten.tech");

        _currentUser = new Mock<ICurrentUserContext>();
        _currentUser.SetupGet(x => x.IsAuthenticated).Returns(false);
        _currentUser.SetupGet(x => x.UserId).Returns(Guid.Empty);

        _eventBus = new Mock<IEventBus>();
        SetupPublish<TenantCreatedV1>(_eventBus);

        // FIX-ONBOARDING — current subscription is read back during the auto-provisioning finalize.
        _tenantSubscriptionRepository.Setup(x => x.GetCurrentByTenantIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid tenantId, CancellationToken _) => new TenantSubscription { TenantId = tenantId, PlanId = Guid.NewGuid() });

        _quotaService = new Mock<IQuotaService>();
        _tenantActivationNotifier = new Mock<ITenantActivationNotifier>();

        _logger = new Mock<ILogger<RegisterTenantCommandHandler>>();

        _handler = new RegisterTenantCommandHandler(
            _repository.Object,
            _subscriptionPlanRepository.Object,
            _tenantSubscriptionRepository.Object,
            _domainRepository.Object,
            _loginSettingsRepository.Object,
            _defaults.Object,
            _currentUser.Object,
            _eventBus.Object,
            _quotaService.Object,
            _tenantActivationNotifier.Object,
            _logger.Object);
    }

    [Fact]
    public async Task RegisterTenant_ShouldInitializeProvisioningAndActivity()
    {
        var result = await _handler.Handle(
            CreateCommand("Acme", "diten.tech", "acme"),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.NotEqual(Guid.Empty, result.Data);
        _repository.Verify(x => x.CreateAsync(It.Is<Tenant>(t =>
            t.Status == TenantStatus.Provisioning &&
            t.ProvisioningStatus == "Started" &&
            t.ProvisioningSteps.Count >= 3 &&
            t.ActivityTimeline.Any(a => a.EventType == "tenant.created") &&
            t.ActivityTimeline.Any(a => a.EventType == "tenant.provisioning.started")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterTenant_ShouldAutoCompleteProvisioning_SeedQuotasAndNotifyAuthService()
    {
        var result = await _handler.Handle(
            CreateCommand("Acme Onboard", "diten.tech", "acme-onboard"),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        // Quota usages seeded from the plan (no QUOTA_USAGE_NOT_FOUND later).
        _quotaService.Verify(x => x.InitializeTenantQuotasAsync(
            result.Data, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        // AuthService told the tenant is active → it provisions roles + syncs entitled-module permissions (FIX-2).
        _tenantActivationNotifier.Verify(x => x.NotifyActivatedAsync(result.Data, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterTenant_ProvisioningFinalizeFailure_DoesNotFailTenantCreation()
    {
        _quotaService.Setup(x => x.InitializeTenantQuotasAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("quota backend down"));

        var result = await _handler.Handle(
            CreateCommand("Acme Resilient", "diten.tech", "acme-resilient"),
            CancellationToken.None);

        // Best-effort: a finalize failure must not fail tenant creation.
        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);
    }

    [Fact]
    public async Task RegisterTenant_ShouldEmitTenantCreatedEvent()
    {
        var command = CreateCommand("Acme Events", "diten.tech", Slug: "acme-events");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccessful);
        _eventBus.Verify(x => x.PublishAsync(
            It.Is<TenantCreatedV1>(e =>
                e.TenantId == result.Data &&
                e.PlanId == command.PlanId &&
                e.TenantDisplayName == "Acme Events" &&
                e.Locale == "en" &&
                e.InitialAdminUserId.HasValue),
            It.Is<EventPublishOptions>(o =>
                o.TenantId == result.Data &&
                o.Producer == "Diten.Platform" &&
                o.OccurredAtUtc.HasValue),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterTenant_ShouldCreatePlatformDomain()
    {
        var result = await _handler.Handle(
            CreateCommand("Acme Corp", "diten.tech"),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        _domainRepository.Verify(x => x.CreateAsync(It.Is<TenantDomain>(d =>
            d.DomainName.EndsWith(".ditenteknoloji.com") &&
            d.IsPrimary &&
            d.IsLoginDomain &&
            d.IsVerified &&
            d.Type == DomainType.Platform &&
            d.Status == TenantDomainStatus.Active), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterTenant_ShouldCreateDefaultLoginSettings()
    {
        var result = await _handler.Handle(
            CreateCommand("Acme Security", "diten.tech", Slug: "acme-security"),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        _loginSettingsRepository.Verify(x => x.CreateAsync(It.Is<TenantLoginSettings>(s =>
            s.TenantRefId == result.Data), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterTenant_WithSlug_ShouldUsePlatformDomain()
    {
        var result = await _handler.Handle(
            CreateCommand("Acme Corp", "diten.tech", Slug: "acme-corp"),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        _repository.Verify(x => x.CreateAsync(It.Is<Tenant>(t =>
            t.Slug == "acme-corp" &&
            t.Domain == "acme-corp.ditenteknoloji.com"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterTenant_WithEnterpriseFields_ShouldMapAllFields()
    {
        var command = new RegisterTenantCommand(
            Name: "Enterprise Corp",
            Domain: "diten.tech",
            Slug: "enterprise",
            DisplayName: "Enterprise Corporation",
            TenantType: TenantType.Customer,
            PlanId: Guid.NewGuid(),
            LegalName: "Enterprise Corp Ltd.",
            TaxNumber: "123456789",
            Country: "TR",
            Industry: "Manufacturing",
            ContactPerson: "John Doe",
            ContactEmail: "john@enterprise.com",
            ContactPhone: "+905551234567",
            DefaultTimezone: "Europe/Istanbul",
            DefaultLanguage: "tr",
            DefaultCurrency: "TRY");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccessful);
        _repository.Verify(x => x.CreateAsync(It.Is<Tenant>(t =>
            t.TenantType == TenantType.Customer &&
            t.PlanId == command.PlanId &&
            t.PlanCode == "FREE" &&
            t.PlanName == "Free" &&
            t.SubscriptionStatus == Diten.Platform.Domain.Enums.TenantSubscriptionStatus.Trialing &&
            t.TrialStartDateUtc.HasValue &&
            t.TrialEndDateUtc.HasValue &&
            t.LegalName == "Enterprise Corp Ltd." &&
            t.TaxNumber == "123456789" &&
            t.Country == "TR" &&
            t.Industry == "Manufacturing" &&
            t.ContactPerson == "John Doe" &&
            t.ContactEmail == "john@enterprise.com" &&
            t.ContactPhone == "+905551234567" &&
            t.DefaultTimezone == "Europe/Istanbul" &&
            t.DefaultLanguage == "tr" &&
            t.DefaultCurrency == "TRY"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterTenant_WithInitialAdmin_ShouldAddInvitationProvisioningStep()
    {
        var command = new RegisterTenantCommand(
            Name: "Admin Test Corp",
            Domain: "diten.tech",
            InitialAdmin: new InitialAdminInfo(
                FirstName: "Jane",
                LastName: "Doe",
                Email: "jane@admintest.com",
                Phone: "+905551234567",
                MfaRequired: true),
            PlanId: Guid.NewGuid());

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccessful);
        _repository.Verify(x => x.UpdateAsync(It.Is<Tenant>(t =>
            t.ProvisioningSteps.Any(s => s.Key == "admin-invitation" && s.Status == "Pending") &&
            t.ActivityTimeline.Any(a => a.EventType == "tenant.admin.invitation.queued")), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RegisterTenant_DuplicateSlug_ShouldThrow()
    {
        _repository.Setup(x => x.GetBySlugAsync("acme", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant
            {
                Code = "ACME1234",
                Slug = "acme",
                Name = "Existing Acme",
                DisplayName = "Existing Acme",
                Domain = "acme.ditenteknoloji.com"
            });

        var result = await _handler.Handle(new RegisterTenantCommand("Acme", "diten.tech", Slug: "acme"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task RegisterTenant_WithPaidPlan_ShouldSetActiveSubscriptionWithoutTrialDates()
    {
        var paidPlanId = Guid.NewGuid();
        _subscriptionPlanRepository.Setup(x => x.GetByIdAsync(paidPlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan
            {
                Id = paidPlanId,
                Code = "STARTER",
                Name = "Starter",
                IsActive = true,
                IsTrialPlan = false
            });

        var result = await _handler.Handle(CreateCommand("Paid Corp", "diten.tech") with { PlanId = paidPlanId }, CancellationToken.None);

        Assert.True(result.IsSuccessful);
        _repository.Verify(x => x.CreateAsync(It.Is<Tenant>(t =>
            t.PlanId == paidPlanId &&
            t.SubscriptionStatus == Diten.Platform.Domain.Enums.TenantSubscriptionStatus.Active &&
            t.TrialStartDateUtc == null &&
            t.TrialEndDateUtc == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterTenant_WithInactivePlan_ShouldThrow()
    {
        var inactivePlanId = Guid.NewGuid();
        _subscriptionPlanRepository.Setup(x => x.GetByIdAsync(inactivePlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan
            {
                Id = inactivePlanId,
                Code = "OLD",
                Name = "Old",
                IsActive = false
            });

        var result = await _handler.Handle(CreateCommand("Inactive Corp", "diten.tech") with { PlanId = inactivePlanId }, CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    // Fresh read copy (like a Mongo deserialize): copies the fields the register/finalize paths touch and gives
    // fresh mutable collections, so updates to a read copy never bleed into the CreateAsync-captured instance.
    private static Tenant CloneForRead(Tenant t) => new()
    {
        Id = t.Id,
        Code = t.Code,
        Slug = t.Slug,
        Name = t.Name,
        DisplayName = t.DisplayName,
        Domain = t.Domain,
        Status = t.Status,
        ProvisioningStatus = t.ProvisioningStatus,
        ProvisionedAt = t.ProvisionedAt,
        ActivatedAt = t.ActivatedAt,
        PlanId = t.PlanId,
        PlanCode = t.PlanCode,
        PlanName = t.PlanName,
        SubscriptionStatus = t.SubscriptionStatus,
        TrialStartDateUtc = t.TrialStartDateUtc,
        TrialEndDateUtc = t.TrialEndDateUtc,
        ProvisioningSteps = t.ProvisioningSteps.Select(s => new TenantProvisioningStep
        {
            Key = s.Key, Label = s.Label, Status = s.Status, CreatedAt = s.CreatedAt, CompletedAt = s.CompletedAt, Detail = s.Detail
        }).ToList(),
        ActivityTimeline = t.ActivityTimeline.Select(a => new TenantActivityEvent
        {
            EventType = a.EventType, Message = a.Message, Actor = a.Actor, At = a.At
        }).ToList()
    };

    private static RegisterTenantCommand CreateCommand(string name, string domain, string? subdomain = null, string? Slug = null) =>
        new(
            Name: name,
            Domain: domain,
            Subdomain: subdomain,
            Slug: Slug,
            DisplayName: name,
            TenantType: TenantType.Customer,
            PlanId: Guid.NewGuid(),
            InitialAdmin: new InitialAdminInfo("Jane", "Doe", $"admin@{name.Replace(" ", string.Empty).ToLowerInvariant()}.com"));

    private static void SetupPublish<TEvent>(Mock<IEventBus> eventBus)
        where TEvent : IIntegrationEvent
    {
        eventBus
            .Setup(x => x.PublishAsync(
                It.IsAny<TEvent>(),
                It.IsAny<EventPublishOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TEvent @event, EventPublishOptions options, CancellationToken _) =>
                new EventEnvelope<TEvent>(
                    new EventMetadata(
                        options.EventId ?? Guid.NewGuid(),
                        @event.EventName,
                        @event.EventVersion,
                        options.CorrelationId ?? Guid.NewGuid(),
                        options.CausationId,
                        options.TenantId,
                        string.IsNullOrWhiteSpace(options.Producer) ? "Diten.Platform.Tests" : options.Producer,
                        options.OccurredAtUtc ?? DateTimeOffset.UtcNow),
                    @event));
    }

    [Fact]
    public async Task SuspendTenant_ShouldRejectDeactivatedTenant()
    {
        var repository = new Mock<ITenantRegistryRepository>();
        repository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant
            {
                Code = "TEN123",
                Slug = "acme",
                Name = "Acme",
                DisplayName = "Acme",
                Domain = "acme.ditenteknoloji.com",
                Status = TenantStatus.Deactivated
            });

        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.IsAuthenticated).Returns(false);

        var eventBus = new Mock<IEventBus>();
        SetupPublish<TenantSuspendedV1>(eventBus);

        var handler = new SuspendTenantCommandHandler(repository.Object, user.Object, eventBus.Object);

        var result = await handler.Handle(new SuspendTenantCommand(Guid.NewGuid(), null), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        eventBus.Verify(x => x.PublishAsync(
            It.IsAny<TenantSuspendedV1>(),
            It.IsAny<EventPublishOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SuspendTenant_ShouldEmitTenantSuspendedEvent()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = "TEN123",
            Slug = "acme",
            Name = "Acme",
            DisplayName = "Acme",
            Domain = "acme.ditenteknoloji.com",
            Status = TenantStatus.Active
        };

        var repository = new Mock<ITenantRegistryRepository>();
        repository.Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        repository.Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.ActorName).Returns("platform-admin");
        user.SetupGet(x => x.UserId).Returns(actorId);

        var eventBus = new Mock<IEventBus>();
        SetupPublish<TenantSuspendedV1>(eventBus);

        var handler = new SuspendTenantCommandHandler(repository.Object, user.Object, eventBus.Object);

        var result = await handler.Handle(new SuspendTenantCommand(tenantId, "billing issue"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        eventBus.Verify(x => x.PublishAsync(
            It.Is<TenantSuspendedV1>(e =>
                e.TenantId == tenantId &&
                e.Reason == "billing issue" &&
                e.SuspendedBy == actorId),
            It.Is<EventPublishOptions>(o =>
                o.TenantId == tenantId &&
                o.Producer == "Diten.Platform" &&
                o.OccurredAtUtc.HasValue),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReactivateTenant_ShouldMoveToActiveAndCompleteProvisioning()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Code = "TEN123",
            Slug = "acme",
            Name = "Acme",
            DisplayName = "Acme",
            Domain = "acme.ditenteknoloji.com",
            Status = TenantStatus.Suspended,
            ProvisioningStatus = "Started",
            ProvisioningSteps =
            [
                new TenantProvisioningStep
                {
                    Key = "bootstrap-platform",
                    Label = "Platform Bootstrap",
                    Status = "InProgress"
                }
            ]
        };

        var repository = new Mock<ITenantRegistryRepository>();
        repository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        repository.Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.IsAuthenticated).Returns(false);

        var eventBus = new Mock<IEventBus>();
        SetupPublish<TenantReactivatedV1>(eventBus);

        var handler = new ReactivateTenantCommandHandler(repository.Object, user.Object, eventBus.Object);
        var result = await handler.Handle(new ReactivateTenantCommand(Guid.NewGuid(), null), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsSuccessful);
        Assert.Equal("Active", result.Data?.Status);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal("Completed", tenant.ProvisioningStatus);
        Assert.Equal("Completed", tenant.ProvisioningSteps[0].Status);
        eventBus.Verify(x => x.PublishAsync(
            It.Is<TenantReactivatedV1>(e => e.TenantId == tenant.Id),
            It.Is<EventPublishOptions>(o => o.TenantId == tenant.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteTenant_ShouldEmitTenantCancelledEvent()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = "TEN123",
            Slug = "acme",
            Name = "Acme",
            DisplayName = "Acme",
            Domain = "acme.ditenteknoloji.com",
            Status = TenantStatus.Suspended
        };

        var repository = new Mock<ITenantRegistryRepository>();
        repository.Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        repository.Setup(x => x.DeleteAsync(tenantId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var tenantDomain = new TenantDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DomainName = "acme.ditenteknoloji.com",
            Type = DomainType.Platform
        };
        var domainRepository = new Mock<ITenantDomainRepository>();
        domainRepository.Setup(x => x.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([tenantDomain]);
        domainRepository.Setup(x => x.DeleteAsync(tenantDomain.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.UserId).Returns(actorId);

        var eventBus = new Mock<IEventBus>();
        SetupPublish<TenantCancelledV1>(eventBus);

        var handler = new DeleteTenantCommandHandler(repository.Object, domainRepository.Object, user.Object, eventBus.Object);

        var result = await handler.Handle(new DeleteTenantCommand(tenantId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        domainRepository.Verify(x => x.DeleteAsync(tenantDomain.Id, It.IsAny<CancellationToken>()), Times.Once);
        eventBus.Verify(x => x.PublishAsync(
            It.Is<TenantCancelledV1>(e =>
                e.TenantId == tenantId &&
                e.EffectiveAtUtc == e.CancelledAtUtc &&
                e.CancelledBy == actorId),
            It.Is<EventPublishOptions>(o =>
                o.TenantId == tenantId &&
                o.Producer == "Diten.Platform" &&
                o.OccurredAtUtc.HasValue),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTenantLoginSettings_ShouldCreateDefault_WhenMissing()
    {
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Code = "TEN123",
            Slug = "acme",
            Name = "Acme",
            DisplayName = "Acme",
            Domain = "acme.ditenteknoloji.com"
        };

        var repository = new Mock<ITenantRegistryRepository>();
        repository.Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);

        var settingsRepository = new Mock<ITenantLoginSettingsRepository>();
        settingsRepository.Setup(x => x.GetByTenantRefIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantLoginSettings?)null);
        settingsRepository.Setup(x => x.CreateAsync(It.IsAny<TenantLoginSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantLoginSettings s, CancellationToken _) => s);

        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.IsAuthenticated).Returns(false);

        var handler = new GetTenantLoginSettingsQueryHandler(repository.Object, settingsRepository.Object, user.Object);
        var result = await handler.Handle(new Features.Tenants.Queries.GetTenantLoginSettingsQuery(tenantId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(tenantId, result.TenantId);
        settingsRepository.Verify(x => x.CreateAsync(It.Is<TenantLoginSettings>(s => s.TenantRefId == tenantId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateTenantLoginSettings_ShouldNormalizeListsAndRecordActivity()
    {
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Code = "TEN123",
            Slug = "acme",
            Name = "Acme",
            DisplayName = "Acme",
            Domain = "acme.ditenteknoloji.com"
        };
        var settings = new TenantLoginSettings { TenantRefId = tenantId };

        var repository = new Mock<ITenantRegistryRepository>();
        repository.Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        repository.Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var settingsRepository = new Mock<ITenantLoginSettingsRepository>();
        settingsRepository.Setup(x => x.GetByTenantRefIdAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        settingsRepository.Setup(x => x.UpdateAsync(It.IsAny<TenantLoginSettings>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.IsAuthenticated).Returns(false);

        var handler = new UpdateTenantLoginSettingsCommandHandler(repository.Object, settingsRepository.Object, user.Object);
        var request = new TenantLoginSettingsUpdateRequest(
            true,
            true,
            true,
            false,
            12,
            true,
            true,
            true,
            true,
            90,
            120,
            14,
            4,
            30,
            true,
            [" 10.0.0.1 ", "", "10.0.0.1"],
            [" tr ", "US"],
            180);

        var result = await handler.Handle(new Features.Tenants.Commands.UpdateTenantLoginSettingsCommand(tenantId, request), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(12, result.PasswordMinLength);
        Assert.Equal(["10.0.0.1"], result.AllowedIps);
        Assert.Equal(["TR", "US"], result.AllowedCountries);
        Assert.Contains(tenant.ActivityTimeline, x => x.EventType == "tenant.login_settings.updated");
        settingsRepository.Verify(x => x.UpdateAsync(It.IsAny<TenantLoginSettings>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
