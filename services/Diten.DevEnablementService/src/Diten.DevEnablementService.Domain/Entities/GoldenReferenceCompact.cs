namespace Diten.DevEnablementService.Domain.Entities;

public sealed class GoldenReferenceCompact : BaseEntity
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
    public int Priority { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}
