using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

public sealed class LegalEntity : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }

    /// <summary>
    /// Optional parent legal entity in the same tenant. Null = root company.
    /// Additive: pre-existing rows have no ParentId and deserialize as null (root).
    /// </summary>
    public Guid? ParentId { get; set; }

    public LegalEntityLifecycleStatus LifecycleStatus { get; set; } = LegalEntityLifecycleStatus.Draft;

    public bool IsReferenceable => LifecycleStatus == LegalEntityLifecycleStatus.Active && !IsDeleted;
}
