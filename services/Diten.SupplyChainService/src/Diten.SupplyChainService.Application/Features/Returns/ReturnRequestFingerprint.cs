using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Diten.SupplyChainService.Domain.Features.Returns;
namespace Diten.SupplyChainService.Application.Features.Returns;
public static class ReturnRequestFingerprint
{
 public static string Create(JsonElement body)
 { var p=ReturnWire.Plan(body);return Hash(JsonSerializer.Serialize(new {p.ShipmentId,p.ReasonCode,Lines=p.Lines.Select(l=>new {l.ShipmentLineNumber,Quantity=ReturnQuantity.Parse(l.Quantity).ToString(),l.UomId}),p.EvidenceReferenceIds})); }
 public static string Transition(JsonElement body)
 {
  var basis=new {TargetStatus=body.GetProperty("targetStatus").GetString(),OccurredAt=ReturnInstant.Normalize(body.GetProperty("occurredAt").GetString()!),InventoryTransactionReferenceId=ReturnWire.OptionalText(body,"inventoryTransactionReferenceId"),DispositionCode=ReturnWire.OptionalText(body,"dispositionCode")};
  // 3.4.0 (MOD-0186 §34): the receipt fields join the fingerprint only when present, so a key stored before them keeps its hash.
  var to=ReturnWire.OptionalText(body,"toWarehouseId");var at=ReturnWire.OptionalText(body,"toLocationId");var unposted=ReturnWire.UnpostedLines(body);
  if(to is null&&at is null&&unposted.Count==0)return Hash(JsonSerializer.Serialize(basis));
  return Hash(JsonSerializer.Serialize(new {basis.TargetStatus,basis.OccurredAt,basis.InventoryTransactionReferenceId,basis.DispositionCode,ToWarehouseId=to,ToLocationId=at,UnpostedLines=unposted.Select(u=>new[]{u.ShipmentLineNumber,u.ReasonCode})}));
 }
 private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
