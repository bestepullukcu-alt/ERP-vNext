using System.Security.Claims;using Diten.SupplyChainService.Application.Common;using Diten.SupplyChainService.Domain.Features.SandopPlans;
namespace Diten.SupplyChainService.Api.Features.SandopPlans;
// Module-local request gate, called by each action until a separately owned Program.cs composition is approved.
public static class SandopContextMiddleware
{ public sealed record Resolution(SandopScope Scope,string Key,Guid Correlation,int Status,string? ErrorCode);
 // G4 item B: S&OP resolves its own context per action, so Program.cs keeps the Shipment catch-all off its paths —
 // otherwise an invalid S&OP request is answered with Shipment-contract errors (the Q271/Q272 shape).
 public static bool IsSandopPath(HttpContext http)=>http.Request.Path.StartsWithSegments("/api/supply-chain/sandop-plans",StringComparison.OrdinalIgnoreCase);
 static bool UniqueUuid(IEnumerable<string?> values,out Guid value)
 {var arr=values.ToArray();value=Guid.Empty;return arr.Length==1&&Guid.TryParseExact(arr[0],"D",out value);}
 static bool Header(HttpRequest r,string name,out Guid value)=>UniqueUuid(r.Headers.TryGetValue(name,out var values)?values.Select(x=>(string?)x):[],out value);
 static bool Claim(ClaimsPrincipal p,string name,out Guid value)=>UniqueUuid(p.FindAll(name).Select(x=>(string?)x.Value),out value)&&value!=Guid.Empty;
 public static async Task<Resolution> ResolveAsync(HttpContext http,string permission,bool mutation)
 { var valid=Header(http.Request,"X-Correlation-Id",out var correlation);if(!valid)correlation=Guid.NewGuid();
 http.Response.Headers["X-Correlation-Id"]=correlation.ToString();
 if(http.User.Identity?.IsAuthenticated!=true)return new(default,"",correlation,401,"UNAUTHENTICATED");
 // R-2 alignment (owner, 2026-10-18): the token carries no legal_entity_id any more (AuthService, R-2 66250b1a1), so only
 // tenant and actor are read from it; requiring the claim refused every real user with 403.
 if(!Claim(http.User,"tenant_id",out var tenant)||!Claim(http.User,"sub",out var actor)||!http.User.HasClaim("permission",permission))return new(default,"",correlation,403,"FORBIDDEN");
 if(!valid)return new(default,"",correlation,400,"INVALID_CORRELATION_ID");
 if(!Header(http.Request,"X-Tenant-Id",out var headerTenant)||!Header(http.Request,"X-Legal-Entity-Id",out var le)||le==Guid.Empty)return new(default,"",correlation,400,"INVALID_REQUEST");
 if(tenant!=headerTenant)return new(default,"",correlation,403,"FORBIDDEN");
 // As the five shipment-bundle modules: X-Legal-Entity-Id is the legal entity's only source and MDM decides it is the
 // tenant's and Active. Every outcome but Valid refuses; a foreign or inactive one answers this module's own not-found.
 switch(await http.RequestServices.GetRequiredService<ILegalEntityScopeValidator>().ValidateAsync(tenant,le,http.Request.Headers.Authorization.ToString(),correlation,http.RequestAborted))
 { case LegalEntityScopeOutcome.NotReferenceable:return new(default,"",correlation,404,"UNKNOWN_SANDOP_PLAN");
   case LegalEntityScopeOutcome.Unavailable:return new(default,"",correlation,503,"DEPENDENCY_UNAVAILABLE"); }
 // G8 L4: the pipeline's logging context gets the resolved scope and correlation, as the Capacity gate does — without it
 // every S&OP log line carried an all-zero CorrelationId/TenantId/LegalEntityId.
 var logging=http.RequestServices.GetService<RequestContext>();
 if(logging is not null){logging.Scope=new(tenant,le,actor);logging.CorrelationId=correlation;}
 string key="";
 if(mutation){var keys=http.Request.Headers["Idempotency-Key"];if(keys.Count!=1||string.IsNullOrEmpty(keys[0]))return new(default,"",correlation,400,"INVALID_REQUEST");key=keys[0]!;}
 return new(new SandopScope(tenant,le,actor),key,correlation,0,null);
 } }
