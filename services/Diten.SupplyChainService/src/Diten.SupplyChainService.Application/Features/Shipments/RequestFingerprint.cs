using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Diten.SupplyChainService.Application.Features.Shipments;
public static class RequestFingerprint
{
    public static string For<T>(T body) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body, ShipmentProjection.Options))));
}
