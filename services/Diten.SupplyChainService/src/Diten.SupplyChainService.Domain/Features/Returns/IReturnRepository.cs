namespace Diten.SupplyChainService.Domain.Features.Returns;
public interface IReturnRepository
{
 Task<IReadOnlyList<ReturnOrder>> QueryAsync(ReturnScope scope, ReturnStatus? status, Guid? shipmentId, CancellationToken ct);
 Task<ReturnMutationResult> MutateAsync(ReturnScope scope, Guid? id, string key, string fingerprint, Guid root,
 ReturnOrder? create, ReturnStatus? target, string? occurredAt, string? inventoryReference, string? dispositionCode,
 Func<ReturnOrder,CancellationToken,Task<ReturnReferenceSnapshot>> observe, CancellationToken ct, string? rawCreateJson = null);
}
