using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Commands.ChangeProductLifecycle;

public sealed class ChangeProductLifecycleCommand : IRequest<Response<bool>>
{
    public Guid Id { get; set; }
    public Guid LifecycleStateId { get; set; }
}
