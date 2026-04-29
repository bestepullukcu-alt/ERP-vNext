using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Commands;

public sealed record UpdateModulePageDefinitionCommand(
    string? ModuleId,
    string? PageCode,
    string PageName,
    string? Description = null,
    string? RoutePath = null,
    string? PageType = null,
    string? RequiredPermissionKey = null,
    bool IsNavigationCandidate = true,
    bool IsActive = true) : IRequest<ModulePageDefinitionDto>;
