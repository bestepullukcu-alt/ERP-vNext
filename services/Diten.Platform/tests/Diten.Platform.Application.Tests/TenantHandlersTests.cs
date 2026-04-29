using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Application.Features.Tenants.Handlers;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests;

public sealed class TenantHandlersTests
{
    private readonly Mock<ITenantRegistryRepository> _repository;
    private readonly Mock<ITenantDomainRepository> _domainRepository;
    private readonly Mock<ITenantLoginSettingsRepository> _loginSettingsRepository;
    private readonly Mock<ITenantDefaultsProvider> _defaults;
    private readonly Mock<ICurrentUserContext> _currentUser;
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
        _repository.Setup(x => x.CreateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        _domainRepository = new Mock<ITenantDomainRepository>();
        _domainRepository.Setup(x => x.GetByDomainNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantDomain?)null);
        _domainRepository.Setup(x => x.CreateAsync(It.IsAny<TenantDomain>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantDomain d, CancellationToken _) => d);

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

        _logger = new Mock<ILogger<RegisterTenantCommandHandler>>();

        _handler = new RegisterTenantCommandHandler(
            _repository.Object,
            _domainRepository.Object,
            _loginSettingsRepository.Object,
            _defaults.Object,
            _currentUser.Object,
            _logger.Object);
    }

    [Fact]
    public async Task RegisterTenant_ShouldInitializeProvisioningAndActivity()
    {
        var id = await _handler.Handle(
            new RegisterTenantCommand("Acme", "diten.tech", "acme"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        _repository.Verify(x => x.CreateAsync(It.Is<Tenant>(t =>
            t.Status == TenantStatus.Provisioning &&
            t.ProvisioningStatus == "Started" &&
            t.ProvisioningSteps.Count >= 3 &&
            t.ActivityTimeline.Any(a => a.EventType == "tenant.created") &&
            t.ActivityTimeline.Any(a => a.EventType == "tenant.provisioning.started")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterTenant_ShouldCreatePlatformDomain()
    {
        var id = await _handler.Handle(
            new RegisterTenantCommand("Acme Corp", "diten.tech"),
            CancellationToken.None);

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
        var id = await _handler.Handle(
            new RegisterTenantCommand("Acme Security", "diten.tech", Slug: "acme-security"),
            CancellationToken.None);

        _loginSettingsRepository.Verify(x => x.CreateAsync(It.Is<TenantLoginSettings>(s =>
            s.TenantRefId == id &&
            s.EmailLoginEnabled &&
            !s.PhoneLoginEnabled &&
            s.PasswordMinLength == 8 &&
            s.SessionTimeoutMinutes == 60 &&
            s.RefreshTokenLifetimeDays == 7), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterTenant_WithSlug_ShouldUsePlatformDomain()
    {
        var id = await _handler.Handle(
            new RegisterTenantCommand("Acme Corp", "diten.tech", Slug: "acme-corp"),
            CancellationToken.None);

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
            TenantType: TenantType.Paid,
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

        var id = await _handler.Handle(command, CancellationToken.None);

        _repository.Verify(x => x.CreateAsync(It.Is<Tenant>(t =>
            t.TenantType == TenantType.Paid &&
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
                MfaRequired: true));

        var id = await _handler.Handle(command, CancellationToken.None);

        _repository.Verify(x => x.UpdateAsync(It.Is<Tenant>(t =>
            t.ProvisioningSteps.Any(s => s.Key == "admin-invitation" && s.Status == "Pending") &&
            t.ActivityTimeline.Any(a => a.EventType == "tenant.admin.invitation.queued")), It.IsAny<CancellationToken>()), Times.Once);
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

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new RegisterTenantCommand("Acme", "diten.tech", Slug: "acme"), CancellationToken.None));
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

        var handler = new SuspendTenantCommandHandler(repository.Object, user.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new SuspendTenantCommand(Guid.NewGuid(), null), CancellationToken.None));
    }

    [Fact]
    public async Task ReactivateTenant_ShouldMoveToActiveAndCompleteProvisioning()
    {
        var tenant = new Tenant
        {
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

        var handler = new ReactivateTenantCommandHandler(repository.Object, user.Object);
        var result = await handler.Handle(new ReactivateTenantCommand(Guid.NewGuid(), null), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Active", result.Status);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal("Completed", tenant.ProvisioningStatus);
        Assert.Equal("Completed", tenant.ProvisioningSteps[0].Status);
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
