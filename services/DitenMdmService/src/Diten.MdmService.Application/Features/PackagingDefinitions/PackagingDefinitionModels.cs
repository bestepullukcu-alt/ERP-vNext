using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Features.PackagingDefinitions;

public abstract class PackagingDefinitionUpsertRequestBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PackagingType PackagingType { get; set; } = PackagingType.Box;
    public PackagingLevel PackagingLevel { get; set; } = PackagingLevel.Primary;
    public Guid? ChildPackagingId { get; set; }
    public int UnitsPerPack { get; set; } = 1;
    public Dimensions? Dimensions { get; set; }
    public Weight? Weight { get; set; }
    public Guid? LifecycleStateId { get; set; }
}

public class PackagingDefinitionListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PackagingType PackagingType { get; set; }
    public PackagingLevel PackagingLevel { get; set; }
    public int UnitsPerPack { get; set; }
    public Guid? LifecycleStateId { get; set; }
}

public sealed class PackagingDefinitionDetailDto : PackagingDefinitionListItemDto
{
    public Guid? ChildPackagingId { get; set; }
    public Dimensions? Dimensions { get; set; }
    public Weight? Weight { get; set; }
}
