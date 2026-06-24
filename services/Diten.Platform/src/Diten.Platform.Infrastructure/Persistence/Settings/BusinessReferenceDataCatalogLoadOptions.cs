namespace Diten.Platform.Infrastructure.Persistence.Settings;

public sealed class BusinessReferenceDataCatalogLoadOptions
{
    public const string SectionName = "BusinessReferenceData:CatalogLoad";

    public bool Enabled { get; set; }
    public string CatalogPath { get; set; } = string.Empty;
    public string TenantId { get; set; } = "00000000-0000-0000-0000-000000000001";
    public string ActorId { get; set; } = "business-reference-data-seed-loader";
    public bool FailOnBlockedConflicts { get; set; } = true;
    public List<string> RequiredSetCodes { get; set; } = [];
}
