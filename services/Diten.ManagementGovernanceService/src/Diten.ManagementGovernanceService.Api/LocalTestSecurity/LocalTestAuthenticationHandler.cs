using System.Security.Claims;
using System.Text.Encodings.Web;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Diten.ManagementGovernanceService.Api.LocalTestSecurity;

public interface ILocalTestIdentitySource { LocalTestIdentity? Resolve(); }
public sealed record LocalTestIdentity(Guid ActorId,Guid TenantId,Guid ServicePrincipalId,IReadOnlyList<string> Permissions);

public sealed class LocalTestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder,
    IServiceProvider services):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    public const string SchemeName="Diten.MOD0355.LocalTest";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var source=services.GetService<ILocalTestIdentitySource>(); if(source?.Resolve() is not { } identity)return Task.FromResult(AuthenticateResult.NoResult());
        if(identity.ActorId==Guid.Empty||identity.TenantId==Guid.Empty||identity.ServicePrincipalId==Guid.Empty||identity.Permissions.Count==0||identity.Permissions.Count!=identity.Permissions.Distinct(StringComparer.Ordinal).Count()||identity.Permissions.Any(x=>!ProcessModelingPermissions.ExactPermissions.Contains(x,StringComparer.Ordinal)))
            return Task.FromResult(AuthenticateResult.Fail("invalid_local_test_identity"));
        var claims=new List<Claim>{new("sub",identity.ActorId.ToString("D")),new("tenant_id",identity.TenantId.ToString("D")),new("service_principal_id",identity.ServicePrincipalId.ToString("D")),new("client_id","diten.management-governance.local-test"),new("module_code","MOD-0355"),new("test_identity","mod0355-local-evidence-v1")};
        claims.AddRange(identity.Permissions.Select(x=>new Claim("permission",x)));
        var principal=new ClaimsPrincipal(new ClaimsIdentity(claims,SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal,SchemeName)));
    }
}
