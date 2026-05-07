using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Commands;

public sealed record UpdateModulePageDescriptorCommand(Guid Id, UpdateModulePageDescriptorRequest Request) : IRequest<Response<NoContent>>;
