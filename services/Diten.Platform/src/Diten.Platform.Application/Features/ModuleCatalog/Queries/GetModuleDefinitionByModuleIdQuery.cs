using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Queries;

public sealed record GetModuleDefinitionByModuleIdQuery(string ModuleId) : IRequest<ModuleDefinitionDetailDto?>;
