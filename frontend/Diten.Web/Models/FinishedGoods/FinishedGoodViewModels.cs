using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.FinishedGoods;

public sealed class CreateFinishedGoodViewModel
{
    [Required]
    public Guid GskuId { get; set; }
}

public class FinishedGoodListItemViewModel
{
    public Guid Id { get; set; }
    public string CanonicalCode { get; set; } = string.Empty;
    public Guid GskuId { get; set; }
    public string GskuCanonicalCode { get; set; } = string.Empty;
    public string LifecycleStatus { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class FinishedGoodDetailViewModel : FinishedGoodListItemViewModel;

public sealed class FinishedGoodGskuSelectorItemViewModel
{
    public Guid Id { get; set; }
    public string GskuCanonicalCode { get; set; } = string.Empty;
}

public sealed class FinishedGoodDraftViewModel
{
    public Guid FinishedGoodId { get; set; }
    public string CanonicalCode { get; set; } = string.Empty;
    public Guid GskuId { get; set; }
    public string GskuCanonicalCode { get; set; } = string.Empty;
    // FG FIX1 item 4 (measured): MDM writes enums as numbers — a string here made every 201 unreadable, so the proxy
    // answered "failed" to a created draft. The value is passed through as MDM sent it.
    public System.Text.Json.JsonElement LifecycleStatus { get; set; }
    public int Version { get; set; }
    public bool BindingReconciliationRequired { get; set; }
}

public sealed class FinishedGoodGatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
