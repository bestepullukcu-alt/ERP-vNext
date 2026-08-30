using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.ProductLegalEntityScopes;

public sealed class ProductLegalEntityScopeWriteViewModel
{
    [Required]
    public Guid GlobalProductId { get; set; }

    [Range(1, 2)]
    public int Mode { get; set; }

    public IReadOnlyList<Guid> LegalEntityIds { get; set; } = [];

    public int? ExpectedVersion { get; set; }
}

public sealed class ProductLegalEntityScopeEndViewModel
{
    [Required]
    public Guid GlobalProductId { get; set; }

    [Range(0, int.MaxValue)]
    public int ExpectedVersion { get; set; }
}

public sealed class ProductLegalEntityScopePeriodViewModel
{
    public Guid PeriodId { get; set; }
    public int Mode { get; set; }
    public IReadOnlyList<Guid> LegalEntityIds { get; set; } = [];
    public DateTimeOffset EffectiveFromUtc { get; set; }
    public DateTimeOffset? EffectiveToUtc { get; set; }
}

public sealed class ProductLegalEntityScopePolicyViewModel
{
    public Guid Id { get; set; }
    public Guid GlobalProductId { get; set; }
    public int Version { get; set; }
    public IReadOnlyList<ProductLegalEntityScopePeriodViewModel> Periods { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class ProductLegalEntityScopeLegalEntityOptionViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class ProductLegalEntityScopeCreateOptionsViewModel
{
    public Guid GlobalProductId { get; set; }
    public string CanonicalCode { get; set; } = string.Empty;
    public string GlobalProductName { get; set; } = string.Empty;
    public IReadOnlyList<ProductLegalEntityScopeLegalEntityOptionViewModel> LegalEntities { get; set; } = [];
}

public sealed class ProductLegalEntityScopeGatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = [];
}
