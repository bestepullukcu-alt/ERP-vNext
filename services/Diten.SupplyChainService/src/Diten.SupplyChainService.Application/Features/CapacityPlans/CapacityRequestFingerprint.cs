using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans;
public static class CapacityRequestFingerprint
{
    public static string Create(string operation, string target, object body)
    {
        var node = JsonSerializer.SerializeToNode(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(operation+"\n"+target+"\n"+Canonical(node)))).ToLowerInvariant();
    }
    private static string Canonical(JsonNode? node) => node switch
    {
        JsonObject obj => "{"+string.Join(",",obj.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>JsonSerializer.Serialize(x.Key)+":"+Canonical(x.Value)))+"}",
        JsonArray arr => "["+string.Join(",",arr.Select(Canonical))+"]",
        null => "null",
        _ => node.ToJsonString()
    };
}
