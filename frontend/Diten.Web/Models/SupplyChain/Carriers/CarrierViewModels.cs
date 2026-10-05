using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.SupplyChain.Carriers;

public sealed class CreateCarrierViewModel
{
    [Required(AllowEmptyStrings = true)]
    [MinLength(1)]
    public string? CarrierCode { get; set; }

    [Required(AllowEmptyStrings = true)]
    [MinLength(1)]
    public string? DisplayName { get; set; }

    [Required]
    [MinLength(1)]
    public List<string>? SupportedModes { get; set; }

    public string? ExternalReference { get; set; }
}

public sealed class ChangeCarrierStatusViewModel
{
    [Required]
    [RegularExpression("^(Active|Suspended|Retired)$")]
    public string? TargetStatus { get; set; }

    // The Carrier contract requires the property but explicitly permits an empty string.
    [Required(AllowEmptyStrings = true)]
    public string? ReasonCode { get; set; }
}
