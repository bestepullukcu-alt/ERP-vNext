using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace Diten.SupplyChainService.Application.Features.Claims;
public static class ClaimRequestFingerprint
{
 private static string Hash(object value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();
 public static string Create(JsonElement body)
 {
  var c=ClaimWire.Plan(body);
  return Hash(new{shipmentId=c.ShipmentId.ToString("D"),carrierId=c.CarrierId?.ToString("D"),reasonCode=c.ReasonCode,claimedAmount=c.ClaimedAmount,currency=c.Currency,evidenceReferenceIds=c.EvidenceReferenceIds});
 }
 public static string Transition(JsonElement body)=>Hash(new{targetStatus=body.GetProperty("targetStatus").GetString(),occurredAt=body.GetProperty("occurredAt").GetString(),resolutionCode=ClaimWire.Optional(body,"resolutionCode"),approvedAmount=ClaimWire.Optional(body,"approvedAmount"),note=ClaimWire.Optional(body,"note")});
}
