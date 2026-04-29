using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Queries;

public sealed record GetModuleDefinitionsQuery(
    string? Search = null,
    Guid? DomainLandscapeId = null,
    Guid? SuitePlatformId = null,
    Guid? CapabilityGroupId = null,
    string? Status = null,
    bool? IsTenantAssignable = null,
    bool? IsPlatformCore = null) : IRequest<ModuleDefinitionListResultDto>;
