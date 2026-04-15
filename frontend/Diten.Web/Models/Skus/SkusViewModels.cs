using System.ComponentModel.DataAnnotations;
using Diten.Web.Models;

namespace Diten.Web.Models.Skus;

public sealed class SkuIndexPageViewModel
{
    public List<LookupApiViewModel> Products { get; set; } = [];
    public List<LookupApiViewModel> Compositions { get; set; } = [];
    public List<LookupApiViewModel> LifecycleStates { get; set; } = [];
}

public sealed class SkuEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public Guid ProductId { get; set; }

    [Required]
    public Guid CompositionId { get; set; }

    public int CompositionVersion { get; set; } = 1;
    public int CompositionRevision { get; set; } = 0;

    [Required]
    public string PackagingForm { get; set; } = string.Empty;

    [Range(0.0001, double.MaxValue)]
    public decimal PackagingQuantity { get; set; }

    public string? Barcode { get; set; }

    [Required]
    public Guid LifecycleStateId { get; set; }

    public List<LookupApiViewModel> Products { get; set; } = [];
    public List<LookupApiViewModel> Compositions { get; set; } = [];
    public List<LookupApiViewModel> LifecycleStates { get; set; } = [];
}

public sealed class SkuSavePayload
{
    public string Code { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public Guid CompositionId { get; set; }
    public int CompositionVersion { get; set; }
    public int CompositionRevision { get; set; }
    public string PackagingForm { get; set; } = string.Empty;
    public decimal PackagingQuantity { get; set; }
    public string? Barcode { get; set; }
    public Guid LifecycleStateId { get; set; }
}

public sealed class SkuApiDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public Guid CompositionId { get; set; }
    public string CompositionCode { get; set; } = string.Empty;
    public string CompositionName { get; set; } = string.Empty;
    public string CompositionVersionLabel { get; set; } = string.Empty;
    public int CompositionVersion { get; set; }
    public int CompositionRevision { get; set; }
    public string PackagingForm { get; set; } = string.Empty;
    public decimal PackagingQuantity { get; set; }
    public string? Barcode { get; set; }
    public Guid LifecycleStateId { get; set; }
    public string LifecycleStateName { get; set; } = string.Empty;
}
