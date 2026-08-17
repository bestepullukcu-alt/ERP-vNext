using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Queries;

public sealed record GetModulePageActionsByPageQuery(Guid PageDescriptorId)
    : IRequest<Response<IReadOnlyList<ModulePageActionDescriptorDto>>>;
