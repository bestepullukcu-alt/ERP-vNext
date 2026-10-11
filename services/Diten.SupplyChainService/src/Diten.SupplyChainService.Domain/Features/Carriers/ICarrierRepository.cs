namespace Diten.SupplyChainService.Domain.Features.Carriers;
public interface ICarrierRepository
{
    Task<IReadOnlyList<Carrier>> QueryAsync(CarrierScope scope, CarrierStatus? status, CancellationToken ct);
    Task<CarrierMutationResult> CreateAsync(CarrierScope scope, string key, string fingerprint, Guid correlation,
        string code, string name, IReadOnlyList<string> modes, string? externalReference, CancellationToken ct);
    Task<CarrierMutationResult> ChangeStatusAsync(CarrierScope scope, Guid id, string key, string fingerprint,
        Guid correlation, CarrierStatus target, string reason, CancellationToken ct);
}
