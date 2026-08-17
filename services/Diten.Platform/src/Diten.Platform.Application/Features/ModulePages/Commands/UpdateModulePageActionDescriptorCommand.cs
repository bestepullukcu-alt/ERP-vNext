using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Commands;

public sealed record UpdateModulePageActionDescriptorCommand(Guid Id, UpdateModulePageActionDescriptorRequest Request)
    : IRequest<Response<NoContent>>;
