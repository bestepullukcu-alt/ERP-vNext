using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

// FEAT-AUDIT-PLATFORM-SECURITY — updating a tenant's login settings changes its entire security posture (MFA,
// password policy, session/lockout, IP-whitelist). Beyond the existing tenant-timeline event, route it to the
// central audit_events store via IAuditableCommand + IAuditMetadataProvider (the RejectWorkflowTaskCommand pattern).
// AfterState carries only the applied policy VALUES (flags/limits) — never PII or raw IP/country list contents.
public sealed record UpdateTenantLoginSettingsCommand(Guid TenantId, TenantLoginSettingsUpdateRequest Request)
    : IRequest<TenantLoginSettingsDto?>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.Security,
        Operation: AuditOperation.Update,
        EntityType: "TenantLoginSettings",
        EntityId: TenantId,
        TargetTenantId: TenantId,
        SourceModule: "tenant-settings",
        AfterState: new Dictionary<string, object?>
        {
            ["twoFactorEnabled"] = Request.TwoFactorEnabled,
            ["mfaRequired"] = Request.MfaRequired,
            ["emailLoginEnabled"] = Request.EmailLoginEnabled,
            ["phoneLoginEnabled"] = Request.PhoneLoginEnabled,
            ["passwordMinLength"] = Request.PasswordMinLength,
            ["passwordRequireUppercase"] = Request.PasswordRequireUppercase,
            ["passwordRequireLowercase"] = Request.PasswordRequireLowercase,
            ["passwordRequireDigit"] = Request.PasswordRequireDigit,
            ["passwordRequireSpecialChar"] = Request.PasswordRequireSpecialChar,
            ["passwordExpirationDays"] = Request.PasswordExpirationDays,
            ["sessionTimeoutMinutes"] = Request.SessionTimeoutMinutes,
            ["refreshTokenLifetimeDays"] = Request.RefreshTokenLifetimeDays,
            ["maxFailedLoginAttempts"] = Request.MaxFailedLoginAttempts,
            ["lockoutDurationMinutes"] = Request.LockoutDurationMinutes,
            // Only the toggle — never the raw AllowedIps / AllowedCountries contents (avoid leaking network topology).
            ["ipWhitelistEnabled"] = Request.IpWhitelistEnabled,
            ["loginAuditRetentionDays"] = Request.LoginAuditRetentionDays,
        });
}
