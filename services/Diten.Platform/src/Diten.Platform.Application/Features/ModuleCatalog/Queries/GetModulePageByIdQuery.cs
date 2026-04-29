using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Queries;

public sealed record GetModulePageByIdQuery(Guid Id) : IRequest<ModulePageDefinitionDto>;
