namespace Diten.Platform.API.Configuration;

public sealed class VerifiedMarketOperationalProvisioningOptions
{
    public const string SectionName = "BusinessReferenceData:VerifiedMarketOperationalProvisioning";
    public const string LockedCatalogFileName = "mod-0290-market-reference.json";
    public const string LockedCatalogVersion = "UNSD-M49-2026-08-08";
    public const string LockedCatalogFingerprint = "b94c45280195b0cb5faa155656c4690938790144d148fba279d2232204360039";
    public bool Enabled { get; set; }
    public string CatalogPath { get; set; } = string.Empty;
    public string ExpectedCatalogVersion { get; set; } = string.Empty;
    public string ExpectedCatalogFingerprint { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string IdempotencyNamespace { get; set; } = string.Empty;
}
