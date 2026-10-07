using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Infrastructure.Services;

/// <inheritdoc cref="ITenantAdminInvitationLedger"/>
public sealed class TenantAdminInvitationLedger(ITenantRegistryRepository tenants) : ITenantAdminInvitationLedger
{
    public const string StepKey = TenantProvisioningStep.AdminInvitationKey;
    private const string Actor = "email-dispatch";

    public async Task RecordUndeliveredAsync(Guid tenantId, string adminEmail, string reasonCode, DateTimeOffset invitationQueuedAt, CancellationToken ct)
    {
        var tenant = await tenants.GetByIdAsync(tenantId, ct);
        var admin = tenant?.AdminUsers.FirstOrDefault(user => string.Equals(user.Email, adminEmail, StringComparison.OrdinalIgnoreCase));
        // FIX2 K5 — only THIS invitation's failure: the administrator must still be Invited, and no newer invitation (a later
        // successful "Invite") may have been sent since this one was queued — that one is the current state, not this.
        if (admin is null || admin.Status != TenantAdminUserStatus.Invited
            || (admin.InvitedAt is { } invitedAt && invitedAt > invitationQueuedAt))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var detail = $"The invitation e-mail could not be delivered ({reasonCode}) and its link cannot be sent again. Use \"Invite\" to send a new link.";
        await tenants.RecordAdminInvitationAsync(
            tenantId, admin.Id, StepKey, "Failed", detail, now, stampInvitedAt: false,
            new TenantActivityEvent { EventType = "tenant.admin_user.invitation_undelivered", Message = detail, At = now, Actor = Actor }, ct);
    }
}
