namespace Diten.AuthService.Application.Common.Interfaces;

public interface ITenantLoginSettingsClient
{
    Task<TenantLoginSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct);
}

public sealed record TenantLoginSettingsSnapshot(
    Guid TenantId,
    bool TwoFactorEnabled,
    bool MfaRequired,
    bool EmailLoginEnabled,
    bool PhoneLoginEnabled,
    int PasswordMinLength,
    bool PasswordRequireUppercase,
    bool PasswordRequireLowercase,
    bool PasswordRequireDigit,
    bool PasswordRequireSpecialChar,
    int? PasswordExpirationDays,
    int SessionTimeoutMinutes,
    int RefreshTokenLifetimeDays,
    int MaxFailedLoginAttempts,
    int LockoutDurationMinutes);
