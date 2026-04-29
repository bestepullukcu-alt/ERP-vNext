using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Commands;

public sealed record CreateModuleDefinitionCommand(
    string ModuleId,
    string ModuleName,
    Guid DomainLandscapeId,
    Guid SuitePlatformId,
    Guid CapabilityGroupId,
    string? DependencyGate = null,
    string? DeliveryOutcome = null,
    string? Placement = null,
    string? SupportModel = null,
    string? Status = null,
    bool IsPlatformCore = false,
    bool IsTenantAssignable = true) : IRequest<ModuleDefinitionDetailDto>;
