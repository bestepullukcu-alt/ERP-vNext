using Diten.SupplyChainService.Domain.Features.Claims;
namespace Diten.SupplyChainService.Application.Features.Claims;
public interface IClaimReferenceReader { Task<ClaimReferenceSnapshot> ObserveAsync(Claim claim,CancellationToken ct); }
