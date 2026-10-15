using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Infrastructure.Eventing;

/// <summary>
/// BL-454 slice 2 stage D FIX1 (3) — with <c>Eventing:Transport=InMemory</c> (the base configuration; BL-536) the event
/// consumers do not run, so a new tenant's first administrator is NOT invited automatically: the tenant's
/// "admin-invitation" step stays Pending and the operator uses "Invite". Outside Development that is said once, at start,
/// as a Warning — never a silent gap. Registered only on the in-memory branch of <c>AddInfrastructure</c>.
/// </summary>
public sealed class TenantAdminInvitationModeNotice(IHostEnvironment environment, ILogger<TenantAdminInvitationModeNotice> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            logger.LogWarning(
                "tenant.admin_invitation.automatic_off Eventing:Transport=InMemory: a new tenant's first administrator is not invited automatically. "
                + "The tenant's \"admin-invitation\" step stays Pending; an operator uses \"Invite\" on the tenant screen.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
