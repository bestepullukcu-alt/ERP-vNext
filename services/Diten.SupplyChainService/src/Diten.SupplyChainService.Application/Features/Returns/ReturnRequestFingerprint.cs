using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Diten.SupplyChainService.Domain.Features.Returns;
namespace Diten.SupplyChainService.Application.Features.Returns;
public static class ReturnRequestFingerprint
{
 public static string Create(JsonElement body)
 { var p=ReturnWire.Plan(body);return Hash(JsonSerializer.Serialize(new {p.ShipmentId,p.ReasonCode,Lines=p.Lines.Select(l=>new {l.ShipmentLineNumber,Quantity=ReturnQuantity.Parse(l.Quantity).ToString(),l.UomId}),p.EvidenceReferenceIds})); }
 public static string Transition(JsonElement body)=>Hash(JsonSerializer.Serialize(new {TargetStatus=body.GetProperty("targetStatus").GetString(),OccurredAt=ReturnInstant.Normalize(body.GetProperty("occurredAt").GetString()!),InventoryTransactionReferenceId=ReturnWire.OptionalText(body,"inventoryTransactionReferenceId"),DispositionCode=ReturnWire.OptionalText(body,"dispositionCode")}));
 private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
