using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.Lskus;

public sealed class CreateLskuViewModel { [Required] public Guid GskuId { get; set; } [Required, StringLength(2)] public string MarketCode { get; set; } = string.Empty; }
public class LskuListItemViewModel { public Guid Id { get; set; } public string CanonicalCode { get; set; } = string.Empty; public Guid GskuId { get; set; } public string GskuCanonicalCode { get; set; } = string.Empty; public string MarketCode { get; set; } = string.Empty; public int LifecycleStatus { get; set; } public int Version { get; set; } public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset? UpdatedAt { get; set; } }
public sealed class LskuDetailViewModel : LskuListItemViewModel { }
public sealed class LskuDraftViewModel { public Guid LskuId { get; set; } public string CanonicalCode { get; set; } = string.Empty; public Guid GskuId { get; set; } public string GskuCanonicalCode { get; set; } = string.Empty; public string MarketCode { get; set; } = string.Empty; public int LifecycleStatus { get; set; } public int Version { get; set; } }
public sealed class LskuCreateOptionsViewModel { public IReadOnlyList<LskuGskuOptionViewModel> Gskus { get; set; } = []; public IReadOnlyList<LskuMarketOptionViewModel> Markets { get; set; } = []; }
public sealed class LskuGskuOptionViewModel { public Guid Id { get; set; } public string CanonicalCode { get; set; } = string.Empty; public string GlobalProductCanonicalCode { get; set; } = string.Empty; public string GlobalProductName { get; set; } = string.Empty; public string RevisionIdentifier { get; set; } = string.Empty; public decimal PackQuantity { get; set; } public string PackUomCode { get; set; } = string.Empty; }
public sealed class LskuMarketOptionViewModel { public string Code { get; set; } = string.Empty; public string DisplayText { get; set; } = string.Empty; public int SortOrder { get; set; } }
public sealed class LskuGatewayResponse<T> { public T? Data { get; set; } public bool IsSuccessful { get; set; } public int StatusCode { get; set; } }
