using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Queries;

public sealed record GetModuleDefinitionByIdQuery(Guid Id) : IRequest<ModuleDefinitionDetailDto?>;
