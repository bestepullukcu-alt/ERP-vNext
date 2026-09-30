namespace Diten.InventoryService.Application.Common;

/// <summary>Constants of the owned frozen contract INVENTORY-BUNDLE v1.0.0 (<c>inventory-bundle.openapi.yaml</c>).</summary>
public static class InventoryContract
{
    /// <summary>Carried by every response body (pack §8 wire format).</summary>
    public const string Version = "v1";

    public const string ModuleId = "MOD-0173";

    public const string BasePath = "/api/inventory";

    public const string IdempotencyKeyHeader = "Idempotency-Key";

    public const string LegalEntityOverrideHeader = "X-Legal-Entity-Id";

    public const string TenantHeader = "X-Tenant-Id";

    /// <summary>Not declared by the frozen contract, therefore optional (pack §8 correlation).</summary>
    public const string CorrelationHeader = "X-Correlation-Id";

    public const int MaxIdempotencyKeyLength = 128;

    public const int MaxPageSize = 200;

    public const int DefaultPageSize = 50;
}
