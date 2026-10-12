using System.Text.Json;
namespace Diten.SupplyChainService.Application.Features.Carriers;
public static class CarrierRequestFingerprint
{
    // Store the canonical representation itself: equality cannot depend on a hash collision.
    public static string Create(CreateCarrierRequest r) => "v1:" + JsonSerializer.Serialize(new { r.CarrierCode, r.DisplayName, r.SupportedModes, r.ExternalReference });
    public static string Status(ChangeCarrierStatusRequest r) => "v1:" + JsonSerializer.Serialize(new { r.TargetStatus, r.ReasonCode });
}
