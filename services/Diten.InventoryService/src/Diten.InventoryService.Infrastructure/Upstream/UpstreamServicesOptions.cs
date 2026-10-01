namespace Diten.InventoryService.Infrastructure.Upstream;

/// <summary>
/// Configuration section <c>Inventory:Upstream</c>. Base URLs come from configuration only (Prism mocks in
/// dev / test, the gateway routes of MOD-0290 and the Location master at progressive integration).
/// </summary>
public sealed class UpstreamServicesOptions
{
    public const string SectionName = "Inventory:Upstream";

    /// <summary>PRODUCT-MASTER-BUNDLE v1 root, e.g. <c>http://127.0.0.1:4010/</c>.</summary>
    public string ProductMasterBaseUrl { get; set; } = string.Empty;

    /// <summary>LOCATION v1 root, e.g. <c>http://127.0.0.1:4011/</c>.</summary>
    public string LocationMasterBaseUrl { get; set; } = string.Empty;

    /// <summary>MDM legal-entity master root through the Gateway, e.g. <c>http://localhost:5000/api/legal-entities/</c> (W-0173-02).</summary>
    public string LegalEntityMasterBaseUrl { get; set; } = string.Empty;

    /// <summary>Per-call timeout; an upstream slower than this is treated as unavailable (fail-closed).</summary>
    public int TimeoutSeconds { get; set; } = 5;
}
