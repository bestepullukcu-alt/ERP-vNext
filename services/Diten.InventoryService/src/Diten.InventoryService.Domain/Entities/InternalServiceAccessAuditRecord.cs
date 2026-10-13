namespace Diten.InventoryService.Domain.Entities;

/// <summary>
/// One service-to-service call of an internal inventory operation (WP-0173-PM2, INVENTORY-BUNDLE 1.1.0 "Audit"): which key
/// identifier, which tenant, which SKU, what answer, which correlation id. Written to <c>inventory_audit</c> next to the
/// posting and reservation audit rows. Only VALIDATED values are stored: a key identifier only when it is a configured
/// one, a tenant / SKU only when it parsed as a UUID — never a raw header, and never a secret.
/// <see cref="EntityBase.LegalEntityId"/> stays null: the operation reads every legal entity of the tenant.
/// </summary>
public sealed class InternalServiceAccessAuditRecord : EntityBase
{
    public const string StockPresenceAction = "INTERNAL_STOCK_PRESENCE_READ";

    public string Action { get; set; } = string.Empty;

    /// <summary>The configured key identifier that authenticated (e.g. <c>ditenmdmservice</c>); null when no key matched.</summary>
    public string? KeyIdentifier { get; set; }

    public Guid? SkuId { get; set; }

    /// <summary>The contract spelling of the asked level, only when it is one of the contract values.</summary>
    public string? SkuLevel { get; set; }

    public int StatusCode { get; set; }

    /// <summary><c>present</c> / <c>absent</c> on 200, otherwise the refusal (<c>unauthorized</c>, <c>invalid-request</c>, <c>unavailable</c>).</summary>
    public string Outcome { get; set; } = string.Empty;

    public Guid CorrelationId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
