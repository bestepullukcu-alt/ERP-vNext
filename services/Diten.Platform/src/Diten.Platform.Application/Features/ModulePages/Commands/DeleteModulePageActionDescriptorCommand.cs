using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Commands;

public sealed record DeleteModulePageActionDescriptorCommand(Guid Id) : IRequest<Response<NoContent>>;
