namespace Diten.DevEnablementService.Domain.Entities;

public sealed class GoldenReferenceSlim : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ReferenceType { get; set; }
    public int Priority { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}
