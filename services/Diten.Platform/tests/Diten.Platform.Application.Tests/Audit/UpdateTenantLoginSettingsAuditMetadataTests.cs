using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Enums;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

// FEAT-AUDIT-PLATFORM-SECURITY — the tenant login-settings update must self-declare its central-audit metadata so the
// AuditBehavior routes it to audit_events as a Security/Update event scoped to the tenant, with policy-only AfterState.
public sealed class UpdateTenantLoginSettingsAuditMetadataTests
{
    [Fact]
    public void GetAuditMetadata_declares_a_tenant_scoped_security_update()
    {
        var tenantId = Guid.NewGuid();
        var command = new UpdateTenantLoginSettingsCommand(tenantId, Request());

        Assert.IsAssignableFrom<IAuditableCommand>(command);
        var metadata = ((IAuditMetadataProvider)command).GetAuditMetadata();

        Assert.Equal(AuditCategory.Security, metadata.Category);
        Assert.Equal(AuditOperation.Update, metadata.Operation);
        Assert.Equal("TenantLoginSettings", metadata.EntityType);
        Assert.Equal(tenantId, metadata.EntityId);
        Assert.Equal(tenantId, metadata.TargetTenantId);
        Assert.Equal("tenant-settings", metadata.SourceModule);

        // AfterState carries the applied policy values, and never the raw IP/country list contents (PII/topology).
        Assert.NotNull(metadata.AfterState);
        Assert.Equal(true, metadata.AfterState!["mfaRequired"]);
        Assert.Equal(12, metadata.AfterState!["passwordMinLength"]);
        Assert.True(metadata.AfterState!.ContainsKey("ipWhitelistEnabled"));
        Assert.False(metadata.AfterState!.ContainsKey("allowedIps"));
        Assert.False(metadata.AfterState!.ContainsKey("allowedCountries"));
    }

    private static TenantLoginSettingsUpdateRequest Request() => new(
        TwoFactorEnabled: true,
        MfaRequired: true,
        EmailLoginEnabled: true,
        PhoneLoginEnabled: false,
        PasswordMinLength: 12,
        PasswordRequireUppercase: true,
        PasswordRequireLowercase: true,
        PasswordRequireDigit: true,
        PasswordRequireSpecialChar: true,
        PasswordExpirationDays: 90,
        SessionTimeoutMinutes: 30,
        RefreshTokenLifetimeDays: 7,
        MaxFailedLoginAttempts: 5,
        LockoutDurationMinutes: 15,
        IpWhitelistEnabled: true,
        AllowedIps: new[] { "10.0.0.1" },
        AllowedCountries: new[] { "TR" },
        LoginAuditRetentionDays: 365);
}
