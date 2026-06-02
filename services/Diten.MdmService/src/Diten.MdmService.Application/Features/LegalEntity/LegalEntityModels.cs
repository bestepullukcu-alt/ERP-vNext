using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Features.LegalEntity;

public sealed record LegalEntityDetailDto(
    Guid LegalEntityId,
    string Code,
    string LegalName,
    string? DisplayName,
    string LifecycleState,
    bool Referenceable);

public sealed record LegalEntityLookupDto(
    Guid LegalEntityId,
    string LegalName,
    string? DisplayName,
    string LifecycleState,
    bool Referenceable);

public static class LegalEntityMappings
{
    public static LegalEntityDetailDto ToDetailDto(Domain.Entities.LegalEntity entity)
        => new(
            entity.Id,
            entity.Code,
            entity.LegalName,
            entity.DisplayName,
            entity.LifecycleStatus.ToString().ToUpperInvariant(),
            entity.IsReferenceable);

    public static LegalEntityLookupDto ToLookupDto(Domain.Entities.LegalEntity entity)
        => new(
            entity.Id,
            entity.LegalName,
            entity.DisplayName,
            entity.LifecycleStatus.ToString().ToUpperInvariant(),
            entity.IsReferenceable);
}
