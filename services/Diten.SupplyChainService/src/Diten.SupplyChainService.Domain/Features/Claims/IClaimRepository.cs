namespace Diten.SupplyChainService.Domain.Features.Claims;
public interface IClaimRepository
{
 Task<IReadOnlyList<Claim>> QueryAsync(ClaimScope scope,ClaimStatus? status,Guid? shipment,CancellationToken ct);
 Task<ClaimMutationResult> MutateAsync(ClaimScope scope,Guid? id,string key,string fingerprint,Guid root,
 Claim? create,ClaimStatus? target,string? occurredAt,string? approvedAmount,string? resolutionCode,string? note,
 Func<Claim,CancellationToken,Task<ClaimReferenceSnapshot>> observe,CancellationToken ct);
}
