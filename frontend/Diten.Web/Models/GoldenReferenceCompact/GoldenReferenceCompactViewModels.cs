using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.GoldenReferenceCompact;

public sealed class GoldenReferenceCompactEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? ReferenceType { get; set; }
    public string? Category { get; set; }
    public string? GroupKey { get; set; }
    public string? SourceSystem { get; set; }
    public string? Owner { get; set; }
    public string? Version { get; set; }
    [DataType(DataType.Date)]
    public DateTime? EffectiveDate { get; set; }
    [DataType(DataType.Date)]
    public DateTime? ExpirationDate { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class GoldenReferenceCompactDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ReferenceType { get; set; }
    public string? Category { get; set; }
    public string? GroupKey { get; set; }
    public string? SourceSystem { get; set; }
    public string? Owner { get; set; }
    public string? Version { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
}

public sealed class GoldenReferenceCompactSavePayload
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ReferenceType { get; set; }
    public string? Category { get; set; }
    public string? GroupKey { get; set; }
    public string? SourceSystem { get; set; }
    public string? Owner { get; set; }
    public string? Version { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
