using System.Net.Http.Headers;
using System.Security.Claims;
using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.Features.Portfolios;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Diten.PpmService.Infrastructure.Portfolios;

// Resolved from the incoming request scope, never from HttpClientFactory's pooled handler scope.
public sealed class PortfolioAuthRequestContext(
    IHttpContextAccessor accessor, ITenantContext tenantContext, ICurrentActorContext actorContext)
{
    private const string BearerScheme = "Bearer";
    private const string TenantHeader = "X-Tenant-Id";
    private static readonly Guid PlatformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public async Task<bool> IsValidAsync(PortfolioAuthorityScope scope, CancellationToken ct) =>
        await ReadTokenAsync(scope, ct) is not null;

    public async Task<bool> TryBindAsync(HttpRequestMessage request, PortfolioAuthorityScope scope, CancellationToken ct)
    {
        var token = await ReadTokenAsync(scope, ct);
        if (token is null) return false;
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, token);
        request.Headers.Remove(TenantHeader);
        request.Headers.Add(TenantHeader, scope.TenantId.ToString("D"));
        return true;
    }

    private async Task<string?> ReadTokenAsync(PortfolioAuthorityScope scope, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var context = accessor.HttpContext;
        if (context is null || scope.ActorId == Guid.Empty || scope.TenantId == Guid.Empty ||
            scope.TenantId == PlatformTenantId || scope.RecordTenantId != scope.TenantId ||
            actorContext.ActorId != scope.ActorId || tenantContext.TenantId != scope.TenantId ||
            !context.Request.Headers.TryGetValue(TenantHeader, out var tenants) || tenants.Count != 1 ||
            !Guid.TryParse(tenants[0], out var headerTenant) || headerTenant != scope.TenantId ||
            !MatchesPrincipal(context.User, scope)) return null;

        AuthenticateResult result;
        try
        {
            // Authenticate explicitly with Bearer: an authenticated cookie or raw header is not evidence.
            result = await context.AuthenticateAsync(BearerScheme).WaitAsync(ct);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        ct.ThrowIfCancellationRequested();
        if (!result.Succeeded || result.Ticket?.AuthenticationScheme != BearerScheme ||
            !MatchesPrincipal(result.Principal, scope)) return null;
        var token = result.Properties?.GetTokenValue("access_token");
        // Only the token saved by the successful authentication ticket may cross this boundary.
        return string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace) || token.Any(char.IsControl)
            ? null : token;
    }

    private static bool MatchesPrincipal(ClaimsPrincipal? principal, PortfolioAuthorityScope scope) =>
        principal?.Identity?.IsAuthenticated == true &&
        MatchesClaims(principal, scope.ActorId, "sub", ClaimTypes.NameIdentifier) &&
        MatchesClaims(principal, scope.TenantId, "tenant_id") &&
        principal.FindAll("actor_type").All(claim => string.Equals(claim.Value, "tenant_user", StringComparison.Ordinal));

    private static bool MatchesClaims(ClaimsPrincipal principal, Guid expected, params string[] types)
    {
        var claims = principal.Claims.Where(claim => types.Contains(claim.Type, StringComparer.Ordinal)).ToArray();
        return claims.Length > 0 && claims.All(claim => Guid.TryParse(claim.Value, out var id) && id == expected);
    }
}
