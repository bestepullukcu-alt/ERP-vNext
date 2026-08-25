namespace Diten.Platform.API.Configuration;

public sealed class VerifiedGskuOperationalProvisioningOptions
{
    public const string SectionName = "BusinessReferenceData:VerifiedGskuOperationalProvisioning";
    public const string LockedCatalogFileName = "mod-0290-gsku-reference.json";
    public const string LockedCatalogVersion = "1.0.0";
    public const string LockedCatalogFingerprint = "e95ef856e87cfaf726b8e4c939e56499791933e69b90bc7fbb6718a949422a5d";

    public bool Enabled { get; set; }
    public bool EnumerationEnabled { get; set; }
    public string CatalogPath { get; set; } = string.Empty;
    public string ExpectedCatalogVersion { get; set; } = string.Empty;
    public string ExpectedCatalogFingerprint { get; set; } = string.Empty;
    public Guid? ConsumerTenantId { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string IdempotencyNamespace { get; set; } = string.Empty;
}
