using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Commands;

public sealed record DeactivateModulePageDescriptorCommand(Guid Id) : IRequest<Response<NoContent>>;
