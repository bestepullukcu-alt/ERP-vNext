using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Queries;

public sealed record GetModulePageActionByIdQuery(Guid Id) : IRequest<Response<ModulePageActionDescriptorDto>>;
