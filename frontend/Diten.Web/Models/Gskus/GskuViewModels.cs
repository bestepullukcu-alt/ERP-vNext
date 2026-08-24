using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.Gskus;

public sealed class CreateGskuViewModel
{
    [Required]
    public Guid GlobalProductId { get; set; }

    [Required]
    public string PackQuantity { get; set; } = string.Empty;

    [Required]
    [StringLength(32)]
    public string PackUomCode { get; set; } = string.Empty;
}

public class GskuListItemViewModel
{
    public Guid Id { get; set; }
    public string CanonicalCode { get; set; } = string.Empty;
    public Guid GlobalProductId { get; set; }
    public string GlobalProductCanonicalCode { get; set; } = string.Empty;
    public string GlobalProductName { get; set; } = string.Empty;
    public Guid ProductDefinitionRevisionId { get; set; }
    public string RevisionIdentifier { get; set; } = string.Empty;
    public decimal PackQuantity { get; set; }
    public string PackUomCode { get; set; } = string.Empty;
    public int LifecycleStatus { get; set; }
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class GskuDetailViewModel : GskuListItemViewModel;

public sealed class GskuDraftViewModel
{
    public Guid GskuId { get; set; }
    public string CanonicalCode { get; set; } = string.Empty;
    public Guid GlobalProductId { get; set; }
    public Guid ProductDefinitionRevisionId { get; set; }
    public string RevisionIdentifier { get; set; } = string.Empty;
    public decimal PackQuantity { get; set; }
    public string PackUomCode { get; set; } = string.Empty;
    public int LifecycleStatus { get; set; }
    public int Version { get; set; }
}

public sealed class GskuCreateOptionsViewModel
{
    public IReadOnlyList<GskuGlobalProductOptionViewModel> GlobalProducts { get; set; } = [];
    public IReadOnlyList<GskuUomOptionViewModel> Uoms { get; set; } = [];
}

public sealed class GskuGlobalProductOptionViewModel
{
    public Guid Id { get; set; }
    public string CanonicalCode { get; set; } = string.Empty;
    public string GlobalProductName { get; set; } = string.Empty;
}

public sealed class GskuUomOptionViewModel
{
    public string Code { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int MaximumDecimalPrecision { get; set; }
}

public sealed class GskuGatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
