using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Infrastructure.Services;

/// <inheritdoc cref="ITenantAdminInvitationLedger"/>
public sealed class TenantAdminInvitationLedger(ITenantRegistryRepository tenants) : ITenantAdminInvitationLedger
{
    public const string StepKey = TenantProvisioningStep.AdminInvitationKey;
    private const string Actor = "email-dispatch";

    public async Task RecordUndeliveredAsync(Guid tenantId, string adminEmail, string reasonCode, Guid dispatchId, CancellationToken ct)
    {
        var tenant = await tenants.GetByIdAsync(tenantId, ct);
        var admin = tenant?.AdminUsers.FirstOrDefault(user => string.Equals(user.Email, adminEmail, StringComparison.OrdinalIgnoreCase));
        if (admin is null)
        {
            return;
        }

        // FIX3 (1) — only THIS invitation's failure, by identity: the repository writes only while the administrator is still
        // Invited and this dispatch is still its current invitation (a later "Invite" names another dispatch).
        var now = DateTimeOffset.UtcNow;
        var detail = $"The invitation e-mail could not be delivered ({reasonCode}) and its link cannot be sent again. Use \"Invite\" to send a new link.";
        await tenants.RecordUndeliveredInvitationAsync(
            tenantId, admin.Id, dispatchId, StepKey, detail, now,
            new TenantActivityEvent { EventType = "tenant.admin_user.invitation_undelivered", Message = detail, At = now, Actor = Actor }, ct);
    }
}
