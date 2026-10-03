using System.Security.Claims;using Diten.SupplyChainService.Domain.Features.SandopPlans;
namespace Diten.SupplyChainService.Api.Features.SandopPlans;
// Module-local request gate, called by each action until a separately owned Program.cs composition is approved.
public static class SandopContextMiddleware
{ public sealed record Resolution(SandopScope Scope,string Key,Guid Correlation,int Status,string? ErrorCode);
 static bool UniqueUuid(IEnumerable<string?> values,out Guid value)
 {var arr=values.ToArray();value=Guid.Empty;return arr.Length==1&&Guid.TryParseExact(arr[0],"D",out value);}
 static bool Header(HttpRequest r,string name,out Guid value)=>UniqueUuid(r.Headers.TryGetValue(name,out var values)?values.Select(x=>(string?)x):[],out value);
 static bool Claim(ClaimsPrincipal p,string name,out Guid value)=>UniqueUuid(p.FindAll(name).Select(x=>(string?)x.Value),out value)&&value!=Guid.Empty;
 public static Resolution Resolve(HttpContext http,string permission,bool mutation)
 { var valid=Header(http.Request,"X-Correlation-Id",out var correlation);if(!valid)correlation=Guid.NewGuid();
 http.Response.Headers["X-Correlation-Id"]=correlation.ToString();
 if(http.User.Identity?.IsAuthenticated!=true)return new(default,"",correlation,401,"UNAUTHENTICATED");
 if(!Claim(http.User,"tenant_id",out var tenant)||!Claim(http.User,"legal_entity_id",out var le)||!Claim(http.User,"sub",out var actor)||!http.User.HasClaim("permission",permission))return new(default,"",correlation,403,"FORBIDDEN");
 if(!valid)return new(default,"",correlation,400,"INVALID_CORRELATION_ID");
 if(!Header(http.Request,"X-Tenant-Id",out var headerTenant)||!Header(http.Request,"X-Legal-Entity-Id",out var headerLe))return new(default,"",correlation,400,"INVALID_REQUEST");
 if(tenant!=headerTenant||le!=headerLe)return new(default,"",correlation,403,"FORBIDDEN");
 string key="";
 if(mutation){var keys=http.Request.Headers["Idempotency-Key"];if(keys.Count!=1||string.IsNullOrEmpty(keys[0]))return new(default,"",correlation,400,"INVALID_REQUEST");key=keys[0]!;}
 return new(new SandopScope(tenant,le,actor),key,correlation,0,null);
 } }
