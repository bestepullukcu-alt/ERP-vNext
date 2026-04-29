using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Application.Features.Tenants.Validators;
using Diten.Platform.Domain.Entities;
using FluentValidation;
using Xunit;

namespace Diten.Platform.Application.Tests;

public sealed class TenantValidatorsTests
{
    private readonly RegisterTenantCommandValidator _validator = new();
    private readonly UpdateTenantLoginSettingsCommandValidator _loginSettingsValidator = new();

    [Fact]
    public void RegisterTenantValidator_ShouldFail_WhenNameOrDomainMissing()
    {
        var result = _validator.Validate(new RegisterTenantCommand("", ""));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void RegisterTenantValidator_ShouldPass_WithMinimalValidInput()
    {
        var result = _validator.Validate(new RegisterTenantCommand("Acme Corp", "diten.tech"));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RegisterTenantValidator_ShouldFail_WhenSlugHasUpperCase()
    {
        var result = _validator.Validate(new RegisterTenantCommand("Acme", "diten.tech", Slug: "AcMe"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Slug");
    }

    [Fact]
    public void RegisterTenantValidator_ShouldFail_WhenSlugHasSpaces()
    {
        var result = _validator.Validate(new RegisterTenantCommand("Acme", "diten.tech", Slug: "acme corp"));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void RegisterTenantValidator_ShouldPass_WithValidSlug()
    {
        var result = _validator.Validate(new RegisterTenantCommand("Acme", "diten.tech", Slug: "acme-corp"));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RegisterTenantValidator_ShouldFail_WhenContactEmailInvalid()
    {
        var result = _validator.Validate(new RegisterTenantCommand("Acme", "diten.tech", ContactEmail: "not-an-email"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ContactEmail");
    }

    [Fact]
    public void RegisterTenantValidator_ShouldFail_WhenCountryCodeInvalid()
    {
        var result = _validator.Validate(new RegisterTenantCommand("Acme", "diten.tech", Country: "XXXX"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Country");
    }

    [Fact]
    public void RegisterTenantValidator_ShouldFail_WhenCurrencyCodeInvalid()
    {
        var result = _validator.Validate(new RegisterTenantCommand("Acme", "diten.tech", DefaultCurrency: "usd"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "DefaultCurrency");
    }

    [Fact]
    public void RegisterTenantValidator_ShouldFail_WhenInitialAdminEmailMissing()
    {
        var command = new RegisterTenantCommand("Acme", "diten.tech",
            InitialAdmin: new InitialAdminInfo("Jane", "Doe", ""));
        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void RegisterTenantValidator_ShouldFail_WhenInitialAdminFirstNameMissing()
    {
        var command = new RegisterTenantCommand("Acme", "diten.tech",
            InitialAdmin: new InitialAdminInfo("", "Doe", "jane@acme.com"));
        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void RegisterTenantValidator_ShouldPass_WithCompleteInitialAdmin()
    {
        var command = new RegisterTenantCommand("Acme", "diten.tech",
            InitialAdmin: new InitialAdminInfo("Jane", "Doe", "jane@acme.com", "+905551234567"));
        var result = _validator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RegisterTenantValidator_ShouldPass_WithAllEnterpriseFields()
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
            DefaultCurrency: "TRY",
            InitialAdmin: new InitialAdminInfo("Jane", "Doe", "jane@enterprise.com"));
        var result = _validator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateTenantLoginSettingsValidator_ShouldFail_WhenRangesInvalid()
    {
        var request = new TenantLoginSettingsUpdateRequest(
            false,
            false,
            true,
            false,
            5,
            true,
            true,
            true,
            true,
            null,
            4,
            366,
            21,
            0,
            false,
            [],
            [],
            0);

        var result = _loginSettingsValidator.Validate(new Features.Tenants.Commands.UpdateTenantLoginSettingsCommand(Guid.Empty, request));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Request.LoginAuditRetentionDays");
    }

    [Fact]
    public void UpdateTenantLoginSettingsValidator_ShouldFail_WhenMfaMismatch()
    {
        var request = CreateValidUpdateReq() with { MfaRequired = true, TwoFactorEnabled = false };
        var result = _loginSettingsValidator.Validate(new Features.Tenants.Commands.UpdateTenantLoginSettingsCommand(Guid.NewGuid(), request));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Two Factor Authentication must be enabled"));
    }

    [Fact]
    public void UpdateTenantLoginSettingsValidator_ShouldFail_WhenNoLoginMethod()
    {
        var request = CreateValidUpdateReq() with { EmailLoginEnabled = false, PhoneLoginEnabled = false };
        var result = _loginSettingsValidator.Validate(new Features.Tenants.Commands.UpdateTenantLoginSettingsCommand(Guid.NewGuid(), request));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("At least one login method must be enabled"));
    }

    [Fact]
    public void UpdateTenantLoginSettingsValidator_ShouldFail_WhenIpWhitelistEmpty()
    {
        var request = CreateValidUpdateReq() with { IpWhitelistEnabled = true, AllowedIps = [] };
        var result = _loginSettingsValidator.Validate(new Features.Tenants.Commands.UpdateTenantLoginSettingsCommand(Guid.NewGuid(), request));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("At least one allowed IP address is required"));
    }

    [Fact]
    public void UpdateTenantLoginSettingsValidator_ShouldFail_WhenInvalidIp()
    {
        var request = CreateValidUpdateReq() with { AllowedIps = ["not-an-ip"] };
        var result = _loginSettingsValidator.Validate(new Features.Tenants.Commands.UpdateTenantLoginSettingsCommand(Guid.NewGuid(), request));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Invalid IP address"));
    }

    [Fact]
    public void UpdateTenantLoginSettingsValidator_ShouldFail_WhenInvalidCountryCode()
    {
        var request = CreateValidUpdateReq() with { AllowedCountries = ["TUR", "t1"] };
        var result = _loginSettingsValidator.Validate(new Features.Tenants.Commands.UpdateTenantLoginSettingsCommand(Guid.NewGuid(), request));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("ISO alpha-2 format"));
    }

    private static TenantLoginSettingsUpdateRequest CreateValidUpdateReq() =>
        new(true, true, true, false, 8, true, true, true, true, 90, 60, 7, 5, 15, false, ["127.0.0.1"], ["TR"], 90);
}
