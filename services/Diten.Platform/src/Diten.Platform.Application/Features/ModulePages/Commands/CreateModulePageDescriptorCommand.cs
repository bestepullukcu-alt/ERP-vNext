using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Commands;

public sealed record CreateModulePageDescriptorCommand(CreateModulePageDescriptorRequest Request) : IRequest<Response<Guid>>;
