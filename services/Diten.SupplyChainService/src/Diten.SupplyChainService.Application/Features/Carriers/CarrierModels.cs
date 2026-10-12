using System.Text.Json.Serialization;
namespace Diten.SupplyChainService.Application.Features.Carriers;
public sealed record CreateCarrierRequest
{
    [JsonRequired] public string? CarrierCode { get; init; }
    [JsonRequired] public string? DisplayName { get; init; }
    [JsonRequired] public List<string>? SupportedModes { get; init; }
    public string? ExternalReference { get; init; }
}
public sealed record ChangeCarrierStatusRequest
{
    [JsonRequired] public string? TargetStatus { get; init; }
    [JsonRequired] public string? ReasonCode { get; init; }
}
public sealed record CarrierResponse(Guid CarrierId, string CarrierCode, string Status, bool IdempotentReplay, string ContractVersion = "v1");
public sealed record CarrierSummary(Guid CarrierId, string CarrierCode, string DisplayName, string Status, IReadOnlyList<string> SupportedModes);
public sealed record CarrierListResponse(IReadOnlyList<CarrierSummary> Items, int Total, string ContractVersion = "v1");
