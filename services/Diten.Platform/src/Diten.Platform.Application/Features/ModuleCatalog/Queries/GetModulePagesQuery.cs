using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Queries;

public sealed record GetModulePagesQuery(string ModuleId) : IRequest<IReadOnlyList<ModulePageDefinitionDto>>;
