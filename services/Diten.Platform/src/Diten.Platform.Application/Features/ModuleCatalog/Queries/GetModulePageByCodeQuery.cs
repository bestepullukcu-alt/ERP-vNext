using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Queries;

public sealed record GetModulePageByCodeQuery(string ModuleId, string PageCode) : IRequest<ModulePageDefinitionDto>;
