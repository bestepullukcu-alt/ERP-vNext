using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

internal static class TenantLoginSettingsMapper
{
    public static TenantLoginSettings CreateDefault(Guid tenantRefId, string actor, DateTimeOffset now)
    {
        return new TenantLoginSettings
        {
            TenantRefId = tenantRefId,
            CreatedBy = actor,
            UpdatedBy = actor,
            UpdatedAt = now
        };
    }

    public static TenantLoginSettingsDto ToDto(TenantLoginSettings settings)
    {
        return new TenantLoginSettingsDto(
            settings.TenantRefId,
            settings.TwoFactorEnabled,
            settings.MfaRequired,
            settings.EmailLoginEnabled,
            settings.PhoneLoginEnabled,
            settings.PasswordMinLength,
            settings.PasswordRequireUppercase,
            settings.PasswordRequireLowercase,
            settings.PasswordRequireDigit,
            settings.PasswordRequireSpecialChar,
            settings.PasswordExpirationDays,
            settings.SessionTimeoutMinutes,
            settings.RefreshTokenLifetimeDays,
            settings.MaxFailedLoginAttempts,
            settings.LockoutDurationMinutes,
            settings.IpWhitelistEnabled,
            settings.AllowedIps,
            settings.AllowedCountries,
            settings.LoginAuditRetentionDays,
            settings.CreatedAt,
            settings.UpdatedAt);
    }

    public static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? values, bool toUpper = false)
    {
        var query = (values ?? [])
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value));

        if (toUpper)
        {
            query = query.Select(value => value.ToUpperInvariant());
        }

        return query.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
