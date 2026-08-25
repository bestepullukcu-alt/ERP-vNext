using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.ValueObjects;

public sealed class ReferenceCatalogSelection
{
    public string SetCode { get; set; } = string.Empty;
    public string ValueCode { get; set; } = string.Empty;
    public Guid CatalogVersionId { get; set; }
    public int CatalogVersionNumber { get; set; }
    public ReferenceCatalogResolutionMode ResolutionMode { get; set; }
    public DateTimeOffset ResolvedAtUtc { get; set; }
}
