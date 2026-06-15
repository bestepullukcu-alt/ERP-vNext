using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

public sealed class LegalEntity : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public LegalEntityLifecycleStatus LifecycleStatus { get; set; } = LegalEntityLifecycleStatus.Draft;

    public bool IsReferenceable => LifecycleStatus == LegalEntityLifecycleStatus.Active && !IsDeleted;
}
