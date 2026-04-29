using Diten.Platform.Common.Persistence;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.Platform.Domain.Entities;

public sealed class DomainLandscape : GlobalEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SuitePlatform : GlobalEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required Guid DomainLandscapeId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CapabilityGroup : GlobalEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required Guid DomainLandscapeId { get; set; }
    public required Guid SuitePlatformId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

[BsonIgnoreExtraElements]
public sealed class ModuleDefinition : GlobalEntity
{
    public required string ModuleId { get; set; }
    public required string ModuleName { get; set; }
    public required Guid DomainLandscapeId { get; set; }
    public required Guid SuitePlatformId { get; set; }
    public required Guid CapabilityGroupId { get; set; }
    public string? DependencyGate { get; set; }
    public string? DeliveryOutcome { get; set; }
    public string? Placement { get; set; }
    public string? SupportModel { get; set; }
    public ModuleLifecycleStatus Status { get; set; } = ModuleLifecycleStatus.Active;
    public bool IsPlatformCore { get; set; }
    public bool IsTenantAssignable { get; set; } = true;
}

public sealed class ModulePageDefinition : GlobalEntity
{
    public required string ModuleId { get; set; }
    public required string PageCode { get; set; }
    public required string PageName { get; set; }
    public string? Description { get; set; }
    public string? RoutePath { get; set; }
    public ModulePageType PageType { get; set; } = ModulePageType.Other;
    public string? RequiredPermissionKey { get; set; }
    public bool IsNavigationCandidate { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public enum ModuleLifecycleStatus
{
    Draft = 0,
    Active = 1,
    Deprecated = 2,
    Retired = 3
}

public enum ModulePageType
{
    List = 0,
    Detail = 1,
    Create = 2,
    Edit = 3,
    Wizard = 4,
    Dashboard = 5,
    Report = 6,
    Admin = 7,
    Other = 8
}
