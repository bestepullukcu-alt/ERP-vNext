using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Infrastructure.Services;

/// <inheritdoc cref="ITenantAdminInvitationLedger"/>
public sealed class TenantAdminInvitationLedger(ITenantRegistryRepository tenants) : ITenantAdminInvitationLedger
{
    public const string StepKey = "admin-invitation";
    private const string Actor = "email-dispatch";

    public async Task RecordUndeliveredAsync(Guid tenantId, string adminEmail, string reasonCode, CancellationToken ct)
    {
        var tenant = await tenants.GetByIdAsync(tenantId, ct);
        if (tenant is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var detail = $"The invitation e-mail could not be delivered ({reasonCode}) and its link cannot be sent again. Use \"Invite\" to send a new link.";
        var step = tenant.ProvisioningSteps.FirstOrDefault(s => s.Key == StepKey);
        if (step is null)
        {
            step = new TenantProvisioningStep { Key = StepKey, Label = "Initial Admin Invitation", CreatedAt = now };
            tenant.ProvisioningSteps.Add(step);
        }

        step.Status = "Failed";
        step.Detail = detail;
        step.CompletedAt = now;
        var admin = tenant.AdminUsers.FirstOrDefault(user => string.Equals(user.Email, adminEmail, StringComparison.OrdinalIgnoreCase));
        if (admin is not null)
        {
            admin.UpdatedAt = now;
        }

        tenant.UpdatedAt = now;
        tenant.UpdatedBy = Actor;
        tenant.ActivityTimeline.Add(new TenantActivityEvent { EventType = "tenant.admin_user.invitation_undelivered", Message = detail, At = now, Actor = Actor });
        await tenants.UpdateAsync(tenant, ct);
    }
}
