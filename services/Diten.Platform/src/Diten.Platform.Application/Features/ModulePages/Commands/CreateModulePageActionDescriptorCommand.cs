using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Commands;

public sealed record CreateModulePageActionDescriptorCommand(Guid PageDescriptorId, CreateModulePageActionDescriptorRequest Request)
    : IRequest<Response<Guid>>;
