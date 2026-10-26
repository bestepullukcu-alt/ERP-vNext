using MediatR;

namespace Diten.SafetyStockService.Application;

public enum PolicyOperation { Create, Revise, Submit, Approve, Reject }
public enum PolicyMutationStatus { Created, Updated, NotFound, Conflict, Invalid, Unavailable }

public sealed record PolicyMutationReceipt(Guid PolicyId, Guid RevisionId, string State, long Version);
public sealed record PolicyMutationResult(PolicyMutationStatus Status, PolicyMutationReceipt? Receipt = null);

public sealed record PolicyMutationCommand(
    PolicyOperation Operation,
    Guid TenantId,
    Guid LegalEntityId,
    Guid ActorId,
    Guid PolicyId,
    Guid RevisionId,
    Guid SkuId,
    Guid WarehouseId,
    int? CoverageDays,
    long? ExpectedVersion,
    string? Reason,
    string IdempotencyKey) : IRequest<PolicyMutationResult>;

public interface IPolicyMutationStore
{
    Task<PolicyMutationResult> ExecuteAsync(PolicyMutationCommand command, ActorSeriesScope scope, CancellationToken cancellationToken);
}
