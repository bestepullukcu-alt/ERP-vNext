namespace Diten.SupplyChainService.Domain.Features.Carriers;
public sealed record CarrierMutationResult(Guid CarrierId, string CarrierCode, string Status, bool IdempotentReplay, int StatusCode, string? ErrorCode = null);
