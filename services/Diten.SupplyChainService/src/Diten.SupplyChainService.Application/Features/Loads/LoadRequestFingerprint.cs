using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
namespace Diten.SupplyChainService.Application.Features.Loads;
public static class LoadRequestFingerprint
{
 public static string Create(JsonElement body) => Hash(JsonSerializer.Serialize(LoadWire.Plan(body)));
 public static string Transition(JsonElement body) => Hash(JsonSerializer.Serialize(new { targetStatus=body.GetProperty("targetStatus").GetString(), occurredAt=LoadWire.NormalizeInstant(body.GetProperty("occurredAt").GetString()!), note=body.TryGetProperty("note",out var n)?n.GetString():null }));
 private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
