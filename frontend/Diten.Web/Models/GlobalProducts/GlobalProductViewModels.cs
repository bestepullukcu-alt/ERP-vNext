using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.GlobalProducts;

public sealed class CreateGlobalProductViewModel
{
    [Required]
    [StringLength(200)]
    public string GlobalProductName { get; set; } = string.Empty;
}

public class GlobalProductListItemViewModel
{
    public Guid Id { get; set; }
    public string CanonicalCode { get; set; } = string.Empty;
    public string GlobalProductName { get; set; } = string.Empty;
    public string LifecycleStatus { get; set; } = string.Empty;
}

public sealed class GlobalProductDetailViewModel : GlobalProductListItemViewModel
{
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class GlobalProductSelectorItemViewModel
{
    public Guid Id { get; set; }
    public string CanonicalCode { get; set; } = string.Empty;
    public string GlobalProductName { get; set; } = string.Empty;
}

public sealed class PagedResultViewModel<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public long TotalCount { get; set; }
}

public sealed class CodeReservationViewModel
{
    public Guid ReservationId { get; set; }
    public string ReservedCode { get; set; } = string.Empty;
    public int Version { get; set; }
}

public sealed class GlobalProductDraftViewModel
{
    public Guid GlobalProductId { get; set; }
    public string CanonicalCode { get; set; } = string.Empty;
    public string GlobalProductName { get; set; } = string.Empty;
    public string LifecycleStatus { get; set; } = string.Empty;
    public int Version { get; set; }
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
