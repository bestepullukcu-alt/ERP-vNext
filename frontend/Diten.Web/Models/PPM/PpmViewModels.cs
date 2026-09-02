using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.PPM;

public sealed class PpmEditViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(64)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public Guid? PortfolioId { get; set; }
    public string? ParentType { get; set; }
    public Guid? ParentId { get; set; }

    [Required]
    public string LifecycleState { get; set; } = string.Empty;

    [StringLength(128)]
    public string? VisibilityPolicyKey { get; set; }

    [Range(1, int.MaxValue)]
    public int Version { get; set; } = 1;
}

public sealed class PpmListPageViewModel
{
    public required string Resource { get; init; }
    public required string Singular { get; init; }
    public required string MarkerType { get; init; }
    public required string TitleKey { get; init; }
    public required string AddKey { get; init; }
    public required string EditKey { get; init; }
    public required string[] LifecycleStates { get; init; }
    public string DefaultLifecycleState { get; init; } = string.Empty;
    public bool HasPortfolio { get; init; }
    public bool RequiresPortfolio { get; init; }
    public bool HasProjectParent { get; init; }
    public bool HasProjectWorkspace { get; init; }
    public bool UsesTitle { get; init; }
    public bool HasInvestmentCaseParent { get; init; }
    public bool HasPlanningDates { get; init; }
    public bool HasBenefitTarget { get; init; }
    public bool ShowsReferenceability { get; init; } = true;
    public bool ShowsVisibilityPolicy { get; init; } = true;
}
