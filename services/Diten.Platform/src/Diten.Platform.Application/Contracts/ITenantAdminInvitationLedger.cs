namespace Diten.Platform.Application.Contracts;

/// <summary>
/// BL-454 slice 2 stage D FIX1 K4 — where an invitation's fate is written on the tenant record. The e-mail retry job calls
/// it when a tenant administrator's invitation can no longer be delivered (its one-time link was never stored, so a retry
/// cannot rebuild it): the tenant's "admin-invitation" step then says so, and that the administrator must be invited again.
/// </summary>
public interface ITenantAdminInvitationLedger
{
    /// <param name="dispatchId">The undeliverable invitation's dispatch. BL-454 stage D FIX3 (1) — it marks the step only
    /// while it is still the administrator's current invitation (<c>LastInvitationDispatchId</c>); a newer "Invite" is the
    /// current state and is left alone. Identity, not clocks: the dispatch is queued BEFORE the invitation time is stamped.</param>
    Task RecordUndeliveredAsync(Guid tenantId, string adminEmail, string reasonCode, Guid dispatchId, CancellationToken ct);
}
