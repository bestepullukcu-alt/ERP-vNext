using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.Compositions;

public sealed class CompositionIndexPageViewModel
{
    public List<LookupApiViewModel> DosageForms { get; set; } = [];
    public List<LookupApiViewModel> LifecycleStates { get; set; } = [];
}

public sealed class CompositionEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    public string FormulationCode { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public int Version { get; set; } = 1;
    public int Revision { get; set; } = 0;

    public string VersionLabel => $"v{Version}";

    [Required]
    public Guid DosageFormId { get; set; }

    [Required]
    [Range(0.000001, double.MaxValue)]
    public decimal? StrengthValue { get; set; }

    [Required]
    public Guid StrengthUnitId { get; set; }

    public decimal? TechnicalFillAmount { get; set; }

    public Guid? TechnicalFillUnitId { get; set; }

    [Required]
    public string LifecycleState { get; set; } = string.Empty;

    public List<CompositionComponentEditViewModel> Components { get; set; } = [];

    public List<LookupApiViewModel> DosageForms { get; set; } = [];
    public List<LookupApiViewModel> LifecycleStates { get; set; } = [];
    public List<LookupApiViewModel> FillUnits { get; set; } = [];
}

public sealed class CompositionDetailsViewModel
{
    public Guid Id { get; set; }
    public string FormulationCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LifecycleState { get; set; } = string.Empty;
    public Guid? CurrentVersionId { get; set; }
    
    public CompositionVersionViewModel? CurrentVersion { get; set; }
    public List<CompositionVersionSummaryViewModel> VersionHistory { get; set; } = [];
}

public class CompositionVersionViewModel
{
    public Guid Id { get; set; }
    public int VersionNo { get; set; }
    public string VersionLabel => $"v{VersionNo}";
    public string Status { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public string DosageFormName { get; set; } = string.Empty;
    public decimal StrengthValue { get; set; }
    public string StrengthUnitName { get; set; } = string.Empty;
    public decimal TechnicalFillAmount { get; set; }
    public string TechnicalFillUnitName { get; set; } = string.Empty;
    public List<CompositionComponentEditViewModel> Components { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
}

public class CompositionVersionSummaryViewModel
{
    public Guid Id { get; set; }
    public int VersionNo { get; set; }
    public string VersionLabel => $"v{VersionNo}";
    public string Status { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class CompositionComponentEditViewModel
{
    public int Sequence { get; set; }
    public Guid ComponentId { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public int ComponentType { get; set; }

    [Range(typeof(decimal), "0.000001", "79228162514264337593543950335")]
    public decimal Quantity { get; set; }

    public Guid UnitId { get; set; }
}

public sealed class LookupApiViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
