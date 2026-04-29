using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities;

public sealed class TenantLoginSettings : GlobalEntity
{
    public Guid TenantRefId { get; set; }

    public bool TwoFactorEnabled { get; set; } = false;
    public bool MfaRequired { get; set; } = false;

    public bool EmailLoginEnabled { get; set; } = true;
    public bool PhoneLoginEnabled { get; set; } = false;

    public int PasswordMinLength { get; set; } = 8;
    public bool PasswordRequireUppercase { get; set; } = true;
    public bool PasswordRequireLowercase { get; set; } = true;
    public bool PasswordRequireDigit { get; set; } = true;
    public bool PasswordRequireSpecialChar { get; set; } = true;
    public int? PasswordExpirationDays { get; set; }

    public int SessionTimeoutMinutes { get; set; } = 60;
    public int RefreshTokenLifetimeDays { get; set; } = 7;

    public int MaxFailedLoginAttempts { get; set; } = 5;
    public int LockoutDurationMinutes { get; set; } = 15;

    public bool IpWhitelistEnabled { get; set; } = false;
    public List<string> AllowedIps { get; set; } = new();

    public List<string> AllowedCountries { get; set; } = new();

    public int LoginAuditRetentionDays { get; set; } = 90;
}
