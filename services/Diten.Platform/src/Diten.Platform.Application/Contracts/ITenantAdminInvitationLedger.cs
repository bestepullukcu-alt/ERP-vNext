namespace Diten.Platform.Application.Contracts;

/// <summary>
/// BL-454 slice 2 stage D FIX1 K4 — where an invitation's fate is written on the tenant record. The e-mail retry job calls
/// it when a tenant administrator's invitation can no longer be delivered (its one-time link was never stored, so a retry
/// cannot rebuild it): the tenant's "admin-invitation" step then says so, and that the administrator must be invited again.
/// </summary>
public interface ITenantAdminInvitationLedger
{
    Task RecordUndeliveredAsync(Guid tenantId, string adminEmail, string reasonCode, CancellationToken ct);
}
