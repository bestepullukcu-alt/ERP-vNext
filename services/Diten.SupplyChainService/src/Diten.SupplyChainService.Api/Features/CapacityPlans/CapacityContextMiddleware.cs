using System.Text.Json;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.CapacityPlans;
using Diten.SupplyChainService.Domain.Features.CapacityPlans;
using Diten.SupplyChainService.Infrastructure.Features.CapacityPlans;
namespace Diten.SupplyChainService.Api.Features.CapacityPlans;
public sealed class CapacityContextMiddleware(RequestDelegate next)
{
    public static bool IsCapacityPath(HttpContext http) =>
        http.Request.Path.StartsWithSegments("/api/supply-chain/capacity-plans",StringComparison.OrdinalIgnoreCase);
    private static bool Uuid(string? text,out Guid value) =>
        Guid.TryParseExact(text,"D",out value) && value!=Guid.Empty;
    private static bool Claim(HttpContext http,string name,out Guid value)
    {
        value=Guid.Empty;var found=http.User.FindAll(name).ToArray();
        return found.Length==1 && Uuid(found[0].Value,out value);
    }
    private static bool UniqueSignedContextFields(string? authorization)
    {
        if(authorization is null || !authorization.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase))return false;
        try
        {
            var segment=authorization[7..].Split('.')[1].Replace('-','+').Replace('_','/');
            segment=segment.PadRight((segment.Length+3)/4*4,'=');
            using var payload=JsonDocument.Parse(Convert.FromBase64String(segment));
            return new[]{"tenant_id","legal_entity_id","sub"}.All(n=>payload.RootElement.EnumerateObject().Count(x=>x.NameEquals(n))==1);
        }
        catch(Exception ex) when(ex is FormatException or JsonException or IndexOutOfRangeException or ArgumentException){return false;}
    }
    public async Task InvokeAsync(HttpContext http,CapacityRequestContext context,RequestContext loggingContext)
    {
        var permission=http.GetEndpoint()?.Metadata.GetMetadata<CapacityPermissionAttribute>();
        if(permission is null){await next(http);return;}
        var corrValues=http.Request.Headers["X-Correlation-Id"];
        Guid parsed=Guid.Empty;
        var validCorrelation=corrValues.Count==1 && Uuid(corrValues[0],out parsed);
        context.CorrelationId=validCorrelation?parsed:Guid.NewGuid();
        http.Response.Headers["X-Correlation-Id"]=context.CorrelationId.ToString();
        async Task Error(int status,string code)
        {
            http.Response.StatusCode=status;
            if(status==401)http.Response.Headers.WWWAuthenticate="Bearer";
            await http.Response.WriteAsJsonAsync(CapacityContractError.Create(code,status,context.CorrelationId));
        }
        if(http.User.Identity?.IsAuthenticated!=true){await Error(401,"UNAUTHENTICATED");return;}
        if(!UniqueSignedContextFields(http.Request.Headers.Authorization.FirstOrDefault()) ||
           !Claim(http,"tenant_id",out var tenant) || !Claim(http,"legal_entity_id",out var le) ||
           !Claim(http,"sub",out var actor) || !http.User.HasClaim("permission",permission.Permission))
        {await Error(403,"FORBIDDEN");return;}
        if(!validCorrelation){await Error(400,"INVALID_CORRELATION_ID");return;}
        if(HttpMethods.IsPost(http.Request.Method))
        {
            var values=http.Request.Headers["Idempotency-Key"];
            if(values.Count!=1 || values[0] is not {Length:>0} key){await Error(400,"INVALID_REQUEST");return;}
            context.IdempotencyKey=key;
            if(!http.Request.HasJsonContentType()){await Error(400,"INVALID_REQUEST");return;}
        }
        if(http.Request.RouteValues.Any(x=>x.Key is "capacityPlanId" or "scenarioId" or "evaluationId" && !Uuid(x.Value?.ToString(),out _)))
        {await Error(400,"INVALID_REQUEST");return;}
        if(http.Request.Query.Keys.Any(x=>x.Equals("tenantId",StringComparison.OrdinalIgnoreCase)||x.Equals("legalEntityId",StringComparison.OrdinalIgnoreCase)))
        {await Error(400,"INVALID_REQUEST");return;}
        context.Scope=new CapacityScope(tenant,le,actor);
        loggingContext.Scope=new(tenant,le,actor);
        loggingContext.CorrelationId=context.CorrelationId;
        await next(http);
    }
}
