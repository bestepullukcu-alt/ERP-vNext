using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Repositories;

public interface IServiceClientOperationalProvisioningOperationRepository
{
    /// <summary>Read-only validation of existing collections/indexes; never creates or repairs storage.</summary>
    Task VerifyStorageAsync(CancellationToken ct);
    /// <summary>Includes deleted/tombstoned command IDs so those IDs cannot be reused.</summary>
    Task<ServiceClientOperationalProvisioningOperation?> GetByCommandIdAsync(Guid commandId, CancellationToken ct);
    Task<bool> TryReserveAsync(ServiceClientOperationalProvisioningOperation operation, CancellationToken ct);
    /// <summary>Sets immutable outcome once, only from Pending/Reserved with no prior outcome.</summary>
    Task<bool> TryCompleteAsync(Guid commandId, string fingerprint, ServiceClientOperationalRecordedOutcome outcome,
        DateTimeOffset nowUtc, CancellationToken ct);
    /// <summary>Never overwrites a completed outcome; uncertainty is not a success receipt.</summary>
    Task<bool> TryMarkRecoveryRequiredAsync(Guid commandId, string fingerprint, CancellationToken ct);
}
