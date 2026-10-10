using Diten.SupplyChainService.Domain.Common;
namespace Diten.SupplyChainService.Domain.Features.Carriers;
public sealed class Carrier : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public string CarrierCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> SupportedModes { get; set; } = [];
    public string? ExternalReference { get; set; }
    public CarrierStatus Status { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}
