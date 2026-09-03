using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Repositories;

public interface IServiceClientOperationalProvisioningOperationRepository
{
    Task<ServiceClientOperationalProvisioningOperation?> GetByCommandIdAsync(Guid commandId, CancellationToken ct);
    Task<bool> TryReserveAsync(ServiceClientOperationalProvisioningOperation operation, CancellationToken ct);
    Task<bool> TryAdvanceAsync(
        Guid commandId,
        string fingerprint,
        ServiceClientOperationalProvisioningCheckpoint expectedCheckpoint,
        ServiceClientOperationalProvisioningCheckpoint nextCheckpoint,
        ServiceClientOperationalProvisioningState nextState,
        ServiceClientOperationalProvisioningEvidence evidence,
        CancellationToken ct);
    Task<bool> TryMarkRecoveryRequiredAsync(
        Guid commandId,
        string fingerprint,
        ServiceClientOperationalProvisioningCheckpoint checkpoint,
        CancellationToken ct);
}
