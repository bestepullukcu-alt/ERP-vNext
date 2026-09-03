using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.PPM;

public sealed class InitiativeEditViewModel
{
    public Guid? Id { get; set; }
    [Required, StringLength(64)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    public Guid? PortfolioId { get; set; }
    [StringLength(128)] public string? InitiativeTypeCode { get; set; }
    [StringLength(128)] public string? PriorityCode { get; set; }
    public DateOnly? PlannedStartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
    [Range(1, int.MaxValue)] public int ExpectedVersion { get; set; } = 1;
}
