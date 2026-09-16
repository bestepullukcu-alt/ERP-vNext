using System.Security.Claims;
using System.Text.Json;
using Diten.SupplyChainService.Domain.Features.Shipments;

namespace Diten.SupplyChainService.Application.Features.SourceIntake.Contracts;

/// <summary>Internal boundary: principal must come from the service's successful JWT validation.</summary>
public sealed class TrustedSourceContext
{
    private TrustedSourceContext(ShipmentScope scope, string bearerToken, Guid? correlationId)
        => (Scope, BearerToken, CorrelationId) = (scope, bearerToken, correlationId);
    public ShipmentScope Scope { get; }
    public string BearerToken { get; }
    public Guid? CorrelationId { get; }

    public static TrustedSourceContext FromValidatedPrincipal(ClaimsPrincipal principal, ShipmentScope scope,
        string bearerToken, Guid? correlationId = null)
    {
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(bearerToken) ||
            bearerToken.Any(char.IsWhiteSpace) || scope.TenantId == Guid.Empty || scope.LegalEntityId == Guid.Empty ||
            scope.ActorId == Guid.Empty || correlationId == Guid.Empty ||
            !Matches(principal, "tenant_id", scope.TenantId) || !Matches(principal, "legal_entity_id", scope.LegalEntityId) ||
            !Matches(principal, "sub", scope.ActorId) || !principal.HasClaim("permission", "supplychain.shipments.create"))
            throw new UnauthorizedAccessException("Validated tenant, legal entity, actor and shipment-create permission required.");
        return new(scope, bearerToken, correlationId);
    }

    private static bool Matches(ClaimsPrincipal principal, string name, Guid expected)
    {
        var claims = principal.FindAll(name).ToArray();
        return claims.Length == 1 && Guid.TryParse(claims[0].Value, out var actual) && actual == expected;
    }
}

public sealed record DependencyResult<T>(bool Success, T? Value, string? ErrorCode, int? StatusCode)
{
    public static DependencyResult<T> Ok(T value) => new(true, value, null, 200);
    public static DependencyResult<T> Blocked(string code, int? status = null) => new(false, default, code, status);
}

public interface IWarehouseReadClient
{
    Task<DependencyResult<JsonElement>> GetAsync(TrustedSourceContext context, string outboundId, CancellationToken cancellationToken = default);
    Task<DependencyResult<JsonElement>> ListAsync(TrustedSourceContext context, string? status = null, string? warehouseId = null,
        string? cursor = null, CancellationToken cancellationToken = default);
}

public sealed record InventoryReference(Guid ItemId, Guid SkuId, string SkuLevel, string? WarehouseId = null,
    string? LocationId = null, string? LotId = null);

public interface IInventoryReadClient
{
    Task<DependencyResult<JsonElement>> AvailabilityAsync(TrustedSourceContext context, InventoryReference reference, CancellationToken cancellationToken = default);
    Task<DependencyResult<JsonElement>> BalanceAsync(TrustedSourceContext context, InventoryReference reference, CancellationToken cancellationToken = default);
}
