using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Diten.SupplyChainService.Application.Features.SandopPlans;
public static class SandopRequestFingerprint
{ public static string Create(JsonElement body)
 { var b=new StringBuilder();Write(body,b);return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(b.ToString()))).ToLowerInvariant(); }
 private static void Write(JsonElement value,StringBuilder b)
 { switch(value.ValueKind) {
 case JsonValueKind.Object: b.Append('{');var first=true;foreach(var p in value.EnumerateObject().OrderBy(x=>x.Name,StringComparer.Ordinal)){if(!first)b.Append(',');first=false;b.Append(JsonSerializer.Serialize(p.Name));b.Append(':');Write(p.Value,b);}b.Append('}');break;
 case JsonValueKind.Array:b.Append('[');var i=0;foreach(var item in value.EnumerateArray()){if(i++>0)b.Append(',');Write(item,b);}b.Append(']');break;
 case JsonValueKind.String:b.Append(JsonSerializer.Serialize(value.GetString()));break;
 case JsonValueKind.Number:b.Append(value.GetRawText());break;
 case JsonValueKind.True:b.Append("true");break;case JsonValueKind.False:b.Append("false");break;case JsonValueKind.Null:b.Append("null");break;
 default:throw new ArgumentException("Unsupported JSON value"); } } }
