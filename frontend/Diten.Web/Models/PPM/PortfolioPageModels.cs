using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.PPM;

public sealed record PortfolioPageModels(bool CanRead, bool CanCreate, int StatusCode)
{
    [Required, StringLength(64)] public string Code { get; init; } = "";
    [Required, StringLength(200)] public string Name { get; init; } = "";
    [StringLength(2000)] public string? Description { get; init; }
    [StringLength(2000)] public string? CapacityAllocationDescription { get; init; }
}
