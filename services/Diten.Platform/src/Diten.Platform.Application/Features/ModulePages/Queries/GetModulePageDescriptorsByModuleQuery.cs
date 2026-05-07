using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Queries;

public sealed record GetModulePageDescriptorsByModuleQuery(string ModuleCode) : IRequest<Response<IReadOnlyList<ModulePageDescriptorListItemDto>>>;
