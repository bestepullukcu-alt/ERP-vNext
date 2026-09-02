using System.Security.Claims;

namespace Diten.ManagementGovernanceService.Api.LocalTestSecurity;

public sealed record ProcessModelingLocalTestActor(Guid TenantId, Guid ActorId, string IdempotencyKey);

public sealed class ProcessModelingLocalTestSecurityException(int statusCode, string reasonCode) : Exception(reasonCode)
{
    public int StatusCode { get; } = statusCode;
    public string ReasonCode { get; } = reasonCode;
}

public static class ProcessModelingLocalTestSecurity
{
    private static readonly string[] TenantClaimTypes = ["diten_tenant_id", "tenant_id", "TenantId"];
    private static readonly string[] ActorClaimTypes = [ClaimTypes.NameIdentifier, "sub"];

    public static ProcessModelingLocalTestActor Resolve(
        ClaimsPrincipal principal,
        string? tenantHeader,
        string requiredPermission,
        string? idempotencyKey,
        bool mutation)
    {
        if (principal.Identity?.IsAuthenticated != true)
            throw new ProcessModelingLocalTestSecurityException(StatusCodes.Status401Unauthorized, "process_modeling_authentication_required");

        var claimTenant = RequiredGuidClaim(principal, TenantClaimTypes);
        if (!Guid.TryParse(tenantHeader, out var requestTenant) || requestTenant == Guid.Empty || requestTenant != claimTenant)
            throw new ProcessModelingLocalTestSecurityException(StatusCodes.Status400BadRequest, "process_modeling_tenant_conflict");

        if (!principal.FindAll("permission").Any(claim => string.Equals(claim.Value, requiredPermission, StringComparison.Ordinal)))
            throw new ProcessModelingLocalTestSecurityException(StatusCodes.Status403Forbidden, "process_modeling_permission_denied");

        var actorId = RequiredGuidClaim(principal, ActorClaimTypes);
        if (mutation && string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ProcessModelingLocalTestSecurityException(StatusCodes.Status400BadRequest, "process_modeling_idempotency_key_required");

        return new(claimTenant, actorId, mutation ? idempotencyKey!.Trim() : string.Empty);
    }

    private static Guid RequiredGuidClaim(ClaimsPrincipal principal, IEnumerable<string> types)
    {
        foreach (var type in types)
            if (Guid.TryParse(principal.FindFirst(type)?.Value, out var value) && value != Guid.Empty)
                return value;
        throw new ProcessModelingLocalTestSecurityException(StatusCodes.Status401Unauthorized, "process_modeling_authentication_required");
    }
}
