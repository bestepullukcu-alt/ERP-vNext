using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Commands;

public sealed record ActivateModulePageDescriptorCommand(Guid Id) : IRequest<Response<NoContent>>;
