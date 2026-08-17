using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.HrisSources;

public sealed class HrisMappingProfile : TenantScopedEntity
{
    public required Guid SourceProfileId { get; set; }
    public required string Code { get; set; }
    public required string DisplayName { get; set; }
    public required string MappingProfileVersion { get; set; }
    public required string ExternalSchemaReference { get; set; }
    public DateTimeOffset? EffectiveFrom { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
