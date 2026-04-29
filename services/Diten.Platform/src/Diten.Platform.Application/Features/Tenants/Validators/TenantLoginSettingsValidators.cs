using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Application.Features.Tenants.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.Tenants.Validators;

public sealed class GetTenantLoginSettingsQueryValidator : AbstractValidator<GetTenantLoginSettingsQuery>
{
    public GetTenantLoginSettingsQueryValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
    }
}

public sealed class UpdateTenantLoginSettingsCommandValidator : AbstractValidator<UpdateTenantLoginSettingsCommand>
{
    public UpdateTenantLoginSettingsCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Request.PasswordMinLength).InclusiveBetween(6, 128);
        RuleFor(x => x.Request.PasswordExpirationDays)
            .InclusiveBetween(1, 3650)
            .When(x => x.Request.PasswordExpirationDays.HasValue);
        RuleFor(x => x.Request.SessionTimeoutMinutes).InclusiveBetween(5, 1440);
        RuleFor(x => x.Request.RefreshTokenLifetimeDays).InclusiveBetween(1, 365);
        RuleFor(x => x.Request.MaxFailedLoginAttempts).InclusiveBetween(1, 20);
        RuleFor(x => x.Request.LockoutDurationMinutes).InclusiveBetween(1, 1440);
        RuleFor(x => x.Request.LoginAuditRetentionDays).InclusiveBetween(1, 3650);

        // Rule 1 — MFA Required requires Two Factor
        RuleFor(x => x.Request.MfaRequired)
            .Must((x, mfaRequired) => !mfaRequired || x.Request.TwoFactorEnabled)
            .WithMessage("Two Factor Authentication must be enabled when MFA is required.");

        // Rule 2 — At least one login method must be enabled
        RuleFor(x => x.Request.EmailLoginEnabled)
            .Must((x, emailEnabled) => emailEnabled || x.Request.PhoneLoginEnabled)
            .WithMessage("At least one login method must be enabled.");

        // Rule 3 — IP whitelist enabled requires allowed IPs
        RuleFor(x => x.Request.AllowedIps)
            .NotEmpty()
            .When(x => x.Request.IpWhitelistEnabled)
            .WithMessage("At least one allowed IP address is required when IP whitelist is enabled.");

        // Rule 4 — AllowedIps must contain valid IP addresses
        RuleForEach(x => x.Request.AllowedIps)
            .Must(BeAValidIpAddress)
            .WithMessage("Invalid IP address: {PropertyValue}");

        // Rule 5 — AllowedCountries must be ISO alpha-2
        RuleForEach(x => x.Request.AllowedCountries)
            .Matches("^[A-Z]{2}$")
            .WithMessage("Invalid country code: {PropertyValue}. Use ISO alpha-2 format.");
    }

    private bool BeAValidIpAddress(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return false;
        return System.Net.IPAddress.TryParse(ip, out _);
    }
}
