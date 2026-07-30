using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Application.Common;

public sealed record ProjectDto(Guid Id, string Code, string Name, string? Description, ProjectParentType ParentType, Guid ParentId, ProjectLifecycleState LifecycleState, string? VisibilityPolicyKey, bool IsReferenceable, int Version);
